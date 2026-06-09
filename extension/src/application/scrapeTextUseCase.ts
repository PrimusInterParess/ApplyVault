import type { ScrapeActiveTabResponse } from '../shared/contracts/messages';
import type { TabExtractionGateway } from './ports/tabExtractionGateway';

export class ScrapeTextUseCase {
  constructor(private readonly tabExtractionGateway: TabExtractionGateway) {}

  async execute(): Promise<ScrapeActiveTabResponse> {
    return this.tabExtractionGateway.extractActiveTab();
  }
}
