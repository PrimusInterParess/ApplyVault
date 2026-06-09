import type { TabExtractionGateway } from '../../application/ports/tabExtractionGateway';
import type { ExtractPageTextResponse } from '../../shared/contracts/messages';
import { getActiveTab, isRestrictedUrl } from './chromeMessagingAdapter';
import { collectFrameExtractionResponses } from './frameExtractionCoordinator';
import { pickBestFrameResult } from './scrapeResultMerger';

export class ChromeTabGateway implements TabExtractionGateway {
  async extractActiveTab(): Promise<ExtractPageTextResponse> {
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

    try {
      const responses = await collectFrameExtractionResponses(activeTab.id);
      return pickBestFrameResult(responses, activeTab);
    } catch (error) {
      return {
        success: false,
        error:
          error instanceof Error ? error.message : 'Chrome could not inject the content script into the active tab.'
      };
    }
  }

  /** @deprecated Use extractActiveTab */
  async requestActiveTabExtraction(): Promise<ExtractPageTextResponse> {
    return this.extractActiveTab();
  }
}
