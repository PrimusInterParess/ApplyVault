import type { ScrapeResult } from '../models/scrapeResult';

export const MessageType = {
  ScrapeActiveTab: 'SCRAPE_ACTIVE_TAB',
  SaveScrapeResult: 'SAVE_SCRAPE_RESULT',
  ExtractPageText: 'EXTRACT_PAGE_TEXT'
} as const;

export type MessageTypeValue = (typeof MessageType)[keyof typeof MessageType];

export interface ScrapeActiveTabRequest {
  type: typeof MessageType.ScrapeActiveTab;
}

export interface ExtractPageTextRequest {
  type: typeof MessageType.ExtractPageText;
}

export interface SaveScrapeResultRequest {
  type: typeof MessageType.SaveScrapeResult;
  payload: ScrapeResult;
}

export type ExtensionRequest =
  | ScrapeActiveTabRequest
  | SaveScrapeResultRequest
  | ExtractPageTextRequest;

export interface SuccessResponse<TData> {
  success: true;
  data: TData;
}

export interface ErrorResponse {
  success: false;
  error: string;
}

export type ExtensionResponse<TData> = SuccessResponse<TData> | ErrorResponse;

export type ScrapeActiveTabResponse = ExtensionResponse<ScrapeResult>;
export type ExtractPageTextResponse = ExtensionResponse<ScrapeResult>;

export interface SaveScrapeResultData {
  id: string;
  savedAt: string;
}

export type SaveScrapeResultResponse = ExtensionResponse<SaveScrapeResultData>;
