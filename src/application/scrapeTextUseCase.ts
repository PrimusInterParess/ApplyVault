import type { ScrapeActiveTabResponse } from '../shared/contracts/messages';
import { ChromeTabGateway } from '../infrastructure/chrome/chromeTabGateway';

export class ScrapeTextUseCase {
  constructor(private readonly chromeTabGateway: ChromeTabGateway) {}

  async execute(): Promise<ScrapeActiveTabResponse> {
    return this.chromeTabGateway.requestActiveTabExtraction();
  }
}
