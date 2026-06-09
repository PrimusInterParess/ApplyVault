import {
  MessageType,
  type SaveScrapeResultRequest,
  type SaveScrapeResultResponse,
  type ScrapeActiveTabRequest,
  type ScrapeActiveTabResponse
} from '../shared/contracts/messages';
import type { ScrapeResult } from '../shared/models/scrapeResult';

export function sendScrapeRequest(): Promise<ScrapeActiveTabResponse> {
  const request: ScrapeActiveTabRequest = {
    type: MessageType.ScrapeActiveTab
  };

  return new Promise((resolve) => {
    chrome.runtime.sendMessage(request, (response?: ScrapeActiveTabResponse) => {
      const runtimeError = chrome.runtime.lastError;

      if (runtimeError) {
        resolve({
          success: false,
          error: runtimeError.message || 'Chrome could not contact the background service worker.'
        });
        return;
      }

      if (!response) {
        resolve({
          success: false,
          error: 'The background service worker did not return a response.'
        });
        return;
      }

      resolve(response);
    });
  });
}

export function sendSaveRequest(payload: ScrapeResult): Promise<SaveScrapeResultResponse> {
  const request: SaveScrapeResultRequest = {
    type: MessageType.SaveScrapeResult,
    payload
  };

  return new Promise((resolve) => {
    chrome.runtime.sendMessage(request, (response?: SaveScrapeResultResponse) => {
      const runtimeError = chrome.runtime.lastError;

      if (runtimeError) {
        resolve({
          success: false,
          error: runtimeError.message || 'Chrome could not contact the background service worker.'
        });
        return;
      }

      if (!response) {
        resolve({
          success: false,
          error: 'The background service worker did not return a response.'
        });
        return;
      }

      resolve(response);
    });
  });
}

export function getActiveTabUrl(): Promise<string | undefined> {
  return new Promise((resolve) => {
    chrome.tabs.query({ active: true, currentWindow: true }, (tabs) => {
      resolve(tabs[0]?.url);
    });
  });
}
