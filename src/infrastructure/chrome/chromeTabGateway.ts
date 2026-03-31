import {
  MessageType,
  type ExtractPageTextRequest,
  type ExtractPageTextResponse
} from '../../shared/contracts/messages';

const RESTRICTED_PROTOCOLS = ['about:', 'brave:', 'chrome:', 'chrome-extension:', 'edge:', 'opera:'];

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

function sendMessageToTab(tabId: number, message: ExtractPageTextRequest): Promise<ExtractPageTextResponse> {
  return new Promise((resolve) => {
    chrome.tabs.sendMessage(tabId, message, (response?: ExtractPageTextResponse) => {
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
        target: { tabId },
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
  return !response.success && response.error.includes('Receiving end does not exist');
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

    const initialResponse = await sendMessageToTab(activeTab.id, request);

    if (!shouldRetryAfterMissingReceiver(initialResponse)) {
      return initialResponse;
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

    return sendMessageToTab(activeTab.id, request);
  }
}
