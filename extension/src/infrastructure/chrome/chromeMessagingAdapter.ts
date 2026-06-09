import type { ExtractPageTextRequest, ExtractPageTextResponse } from '../../shared/contracts/messages';

const RESTRICTED_PROTOCOLS = ['about:', 'brave:', 'chrome:', 'chrome-extension:', 'edge:', 'opera:'];

export interface TabFrame {
  frameId: number;
  url?: string;
}

export function isRestrictedUrl(url?: string): boolean {
  if (!url) {
    return false;
  }

  return RESTRICTED_PROTOCOLS.some((protocol) => url.startsWith(protocol));
}

export function getActiveTab(): Promise<chrome.tabs.Tab | undefined> {
  return new Promise((resolve) => {
    chrome.tabs.query({ active: true, currentWindow: true }, (tabs) => {
      resolve(tabs[0]);
    });
  });
}

export function getTabFrames(tabId: number): Promise<TabFrame[]> {
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

export function sendMessageToFrame(
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

export function injectContentScript(tabId: number): Promise<void> {
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
