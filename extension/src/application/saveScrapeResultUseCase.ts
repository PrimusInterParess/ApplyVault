import type {
  SaveScrapeResultResponse
} from '../shared/contracts/messages';
import type { ScrapeResult } from '../shared/models/scrapeResult';
import type { ScrapeResultGateway } from '../infrastructure/api/aspNetApiClient';

export class SaveScrapeResultUseCase {
  constructor(private readonly scrapeResultGateway: ScrapeResultGateway) {}

  async execute(result: ScrapeResult): Promise<SaveScrapeResultResponse> {
    const savedResult = await this.scrapeResultGateway.send(result);

    return {
      success: true,
      data: savedResult
    };
  }
}
