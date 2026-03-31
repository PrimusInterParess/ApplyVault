import { ScrapeTextUseCase } from '../application/scrapeTextUseCase';
import { SaveScrapeResultUseCase } from '../application/saveScrapeResultUseCase';
import { AspNetApiClient } from '../infrastructure/api/aspNetApiClient';
import { ChromeTabGateway } from '../infrastructure/chrome/chromeTabGateway';
import {
  MessageType,
  type ExtensionRequest,
  type SaveScrapeResultResponse,
  type ScrapeActiveTabResponse
} from '../shared/contracts/messages';

const apiClient = new AspNetApiClient();
const scrapeTextUseCase = new ScrapeTextUseCase(new ChromeTabGateway());
const saveScrapeResultUseCase = new SaveScrapeResultUseCase(apiClient);

chrome.runtime.onMessage.addListener(
  (
    message: ExtensionRequest,
    _sender,
    sendResponse: (response: ScrapeActiveTabResponse | SaveScrapeResultResponse) => void
  ) => {
    if (message.type === MessageType.ScrapeActiveTab) {
      void scrapeTextUseCase
        .execute()
        .then(sendResponse)
        .catch((error) => {
          sendResponse({
            success: false,
            error: error instanceof Error ? error.message : 'The scrape flow failed.'
          });
        });

      return true;
    }

    if (message.type === MessageType.SaveScrapeResult) {
      void saveScrapeResultUseCase
        .execute(message.payload)
        .then(sendResponse)
        .catch((error) => {
          sendResponse({
            success: false,
            error: error instanceof Error ? error.message : 'Saving the scrape result failed.'
          });
        });

      return true;
    }

    return false;
  }
);
