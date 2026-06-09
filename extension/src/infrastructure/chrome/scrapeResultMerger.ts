import type { ExtractPageTextResponse } from '../../shared/contracts/messages';
import type { ScrapeResult } from '../../shared/models/scrapeResult';
import { evaluateScrapeResult, getScrapeResultScore } from '../../shared/utils/scrapeQuality';
import type { TabFrame } from './chromeMessagingAdapter';

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

export function mergeScrapeResults(primary: ScrapeResult, fallback?: ScrapeResult): ScrapeResult {
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
      ),
      fieldSources: primary.jobDetails.fieldSources ?? fallback.jobDetails.fieldSources,
      extractorVersion: primary.jobDetails.extractorVersion ?? fallback.jobDetails.extractorVersion
    }
  };

  return mergedResult;
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

export function selectBestFrameExtractionResult(
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
