import type { ExtractPageTextResponse } from '../../shared/contracts/messages';

export interface TabExtractionGateway {
  extractActiveTab(): Promise<ExtractPageTextResponse>;
}
