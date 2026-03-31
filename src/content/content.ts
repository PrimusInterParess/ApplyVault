import {
  MessageType,
  type ExtensionRequest,
  type ExtractPageTextResponse
} from '../shared/contracts/messages';
import { extractVisibleText } from './extractVisibleText';

function createErrorResponse(message: string): ExtractPageTextResponse {
  return {
    success: false,
    error: message
  };
}

chrome.runtime.onMessage.addListener(
  (message: ExtensionRequest, _sender, sendResponse: (response: ExtractPageTextResponse) => void) => {
    if (message.type !== MessageType.ExtractPageText) {
      return false;
    }

    try {
      const result = extractVisibleText(document);

      sendResponse({
        success: true,
        data: result
      });
    } catch (error) {
      const messageText = error instanceof Error ? error.message : 'Text extraction failed.';
      sendResponse(createErrorResponse(messageText));
    }

    return false;
  }
);
