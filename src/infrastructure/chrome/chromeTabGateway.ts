import {
  MessageType,
  type ExtractPageTextRequest,
  type ExtractPageTextResponse
} from '../../shared/contracts/messages';
import type { ScrapeResult } from '../../shared/models/scrapeResult';
import { evaluateScrapeResult, getScrapeResultScore } from '../../shared/utils/scrapeQuality';

const RESTRICTED_PROTOCOLS = ['about:', 'brave:', 'chrome:', 'chrome-extension:', 'edge:', 'opera:'];
const MISSING_RECEIVER_ERROR = 'Receiving end does not exist';

interface TabFrame {
  frameId: number;
  url?: string;
}

function isRestrictedUrl(url?: string): boolean {
  if (!url) {
    return false;
  }

  return RESTRICTED_PROTOCOLS.some((protocol) => url.startsWith(protocol));
}

function getActiveTab(): Promise<chrome.tabs.Tab | undefined> {
  return new Promise((resolve) => {
    chrome.tabs.query({ active: true, currentWindow: true }, (tabs) => {
      resolve(tabs[0]);
    });
  });
}

function getHostname(url?: string): string | undefined {
  if (!url) {
    return undefined;
  }

  try {
    return new URL(url).hostname;
  } catch {
    return undefined;
  }
}

function getTabFrames(tabId: number): Promise<TabFrame[]> {
  return new Promise((resolve) => {
    chrome.webNavigation.getAllFrames({ tabId }, (details) => {
      const runtimeError = chrome.runtime.lastError;

      if (runtimeError || !details || details.length === 0) {
        resolve([{ frameId: 0 }]);
        return;
      }

      resolve(
        details.map((detail) => ({
          frameId: detail.frameId,
          url: detail.url
        }))
      );
    });
  });
}

function sendMessageToFrame(
  tabId: number,
  frameId: number,
  message: ExtractPageTextRequest
): Promise<ExtractPageTextResponse> {
  return new Promise((resolve) => {
    chrome.tabs.sendMessage(tabId, message, { frameId }, (response?: ExtractPageTextResponse) => {
      const runtimeError = chrome.runtime.lastError;

      if (runtimeError) {
        resolve({
          success: false,
          error: runtimeError.message || 'Chrome could not reach the content script.'
        });
        return;
      }

      if (!response) {
        resolve({
          success: false,
          error: 'The content script did not return a response.'
        });
        return;
      }

      resolve(response);
    });
  });
}

function injectContentScript(tabId: number): Promise<void> {
  return new Promise((resolve, reject) => {
    chrome.scripting.executeScript(
      {
        target: { tabId, allFrames: true },
        files: ['content/content.js']
      },
      () => {
        const runtimeError = chrome.runtime.lastError;

        if (runtimeError) {
          reject(new Error(runtimeError.message || 'Chrome could not inject the content script.'));
          return;
        }

        resolve();
      }
    );
  });
}

function shouldRetryAfterMissingReceiver(response: ExtractPageTextResponse): boolean {
  return !response.success && response.error.includes(MISSING_RECEIVER_ERROR);
}

async function requestExtractionFromFrames(
  tabId: number,
  message: ExtractPageTextRequest
): Promise<Array<{ frame: TabFrame; response: ExtractPageTextResponse }>> {
  const frames = await getTabFrames(tabId);

  return Promise.all(
    frames.map(async (frame) => ({
      frame,
      response: await sendMessageToFrame(tabId, frame.frameId, message)
    }))
  );
}

function mergeTexts(primaryText: string, fallbackText: string): string {
  const normalizedPrimaryText = primaryText.trim();
  const normalizedFallbackText = fallbackText.trim();

  if (!normalizedFallbackText) {
    return normalizedPrimaryText;
  }

  if (!normalizedPrimaryText) {
    return normalizedFallbackText;
  }

  if (
    normalizedPrimaryText === normalizedFallbackText ||
    normalizedPrimaryText.includes(normalizedFallbackText)
  ) {
    return normalizedPrimaryText;
  }

  if (normalizedFallbackText.includes(normalizedPrimaryText)) {
    return normalizedFallbackText;
  }

  return `${normalizedPrimaryText}\n\n${normalizedFallbackText}`;
}

function mergeContacts(
  primaryContacts: ScrapeResult['jobDetails']['hiringManagerContacts'],
  fallbackContacts: ScrapeResult['jobDetails']['hiringManagerContacts']
): ScrapeResult['jobDetails']['hiringManagerContacts'] {
  const mergedContacts = new Map<string, ScrapeResult['jobDetails']['hiringManagerContacts'][number]>();

  for (const contact of [...primaryContacts, ...fallbackContacts]) {
    const key = `${contact.type}|${contact.value.toLowerCase()}|${contact.label?.toLowerCase() ?? ''}`;

    if (!mergedContacts.has(key)) {
      mergedContacts.set(key, contact);
    }
  }

  return Array.from(mergedContacts.values());
}

function mergeScrapeResults(primary: ScrapeResult, fallback?: ScrapeResult): ScrapeResult {
  if (!fallback) {
    return primary;
  }

  const mergedText = mergeTexts(primary.text, fallback.text);
  const mergedResult: ScrapeResult = {
    ...primary,
    title: primary.title || fallback.title,
    text: mergedText,
    textLength: mergedText.length,
    jobDetails: {
      ...primary.jobDetails,
      detectedPageType:
        primary.jobDetails.detectedPageType !== 'generic-page'
          ? primary.jobDetails.detectedPageType
          : fallback.jobDetails.detectedPageType,
      jobTitle: primary.jobDetails.jobTitle ?? fallback.jobDetails.jobTitle,
      companyName: primary.jobDetails.companyName ?? fallback.jobDetails.companyName,
      location: primary.jobDetails.location ?? fallback.jobDetails.location,
      jobDescription: primary.jobDetails.jobDescription ?? fallback.jobDetails.jobDescription,
      positionSummary: primary.jobDetails.positionSummary ?? fallback.jobDetails.positionSummary,
      hiringManagerName: primary.jobDetails.hiringManagerName ?? fallback.jobDetails.hiringManagerName,
      hiringManagerContacts: mergeContacts(
        primary.jobDetails.hiringManagerContacts,
        fallback.jobDetails.hiringManagerContacts
      )
    }
  };

  return {
    ...mergedResult,
    extraction: {
      ...evaluateScrapeResult(mergedResult),
      attempts: Math.max(primary.extraction?.attempts ?? 1, fallback.extraction?.attempts ?? 1)
    }
  };
}

function applyActiveTabContext(result: ScrapeResult, activeTab: chrome.tabs.Tab): ScrapeResult {
  const activeTabUrl = activeTab.url ?? result.url;
  const sourceHostname = getHostname(activeTabUrl) ?? result.jobDetails.sourceHostname;
  const contextualizedResult: ScrapeResult = {
    ...result,
    url: activeTabUrl,
    jobDetails: {
      ...result.jobDetails,
      sourceHostname
    }
  };

  return {
    ...contextualizedResult,
    extraction: {
      ...evaluateScrapeResult(contextualizedResult),
      attempts: result.extraction?.attempts ?? 1
    }
  };
}

function pickBestFrameResult(
  responses: Array<{ frame: TabFrame; response: ExtractPageTextResponse }>,
  activeTab: chrome.tabs.Tab
): ExtractPageTextResponse {
  const successfulResponses = responses.filter(
    (entry): entry is { frame: TabFrame; response: { success: true; data: ScrapeResult } } => entry.response.success
  );

  if (successfulResponses.length === 0) {
    return responses[0]?.response ?? {
      success: false,
      error: 'The content script did not return a response.'
    };
  }

  const bestResponse = successfulResponses
    .map((entry) => ({
      ...entry,
      score: getScrapeResultScore(entry.response.data)
    }))
    .sort((left, right) => right.score - left.score)[0];
  const mainFrameResponse = successfulResponses.find((entry) => entry.frame.frameId === 0)?.response.data;
  const mergedResult = mergeScrapeResults(bestResponse.response.data, mainFrameResponse);

  return {
    success: true,
    data: applyActiveTabContext(mergedResult, activeTab)
  };
}

function shouldRetryAfterMissingReceivers(
  responses: Array<{ frame: TabFrame; response: ExtractPageTextResponse }>
): boolean {
  return responses.length > 0 && responses.every((entry) => shouldRetryAfterMissingReceiver(entry.response));
}

export class ChromeTabGateway {
  async requestActiveTabExtraction(): Promise<ExtractPageTextResponse> {
    const activeTab = await getActiveTab();

    if (!activeTab?.id) {
      return {
        success: false,
        error: 'No active browser tab was found.'
      };
    }

    if (isRestrictedUrl(activeTab.url)) {
      return {
        success: false,
        error: 'This page does not allow Chrome extensions to inject a scraper.'
      };
    }

    const request: ExtractPageTextRequest = {
      type: MessageType.ExtractPageText
    };

    const initialResponses = await requestExtractionFromFrames(activeTab.id, request);

    if (!shouldRetryAfterMissingReceivers(initialResponses)) {
      return pickBestFrameResult(initialResponses, activeTab);
    }

    try {
      await injectContentScript(activeTab.id);
    } catch (error) {
      return {
        success: false,
        error:
          error instanceof Error ? error.message : 'Chrome could not inject the content script into the active tab.'
      };
    }

    const retriedResponses = await requestExtractionFromFrames(activeTab.id, request);
    return pickBestFrameResult(retriedResponses, activeTab);
  }
}
