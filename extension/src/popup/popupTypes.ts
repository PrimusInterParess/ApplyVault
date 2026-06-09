import type { ScrapeResult } from '../shared/models/scrapeResult';

export type PopupMode = 'scrape' | 'save';
export type EditableControl = HTMLInputElement | HTMLTextAreaElement;
export type AuthStatusTone = 'default' | 'error';

export interface PopupDraft {
  mode: PopupMode;
  result: ScrapeResult;
  statusMessage: string;
}

export interface PopupDetailsElements {
  scrapedAt: EditableControl;
  sourceHostname: EditableControl;
  pageType: EditableControl;
  jobTitle: EditableControl;
  companyName: EditableControl;
  jobLocation: EditableControl;
  hiringManager: EditableControl;
  positionSummary: EditableControl;
  contacts: HTMLTextAreaElement;
}

export interface PopupResultPanels {
  details: HTMLElement;
  contacts: HTMLElement;
  description: HTMLElement;
  text: HTMLElement;
}

export const POPUP_DRAFT_STORAGE_KEY = 'popupDraft';
export const DEFAULT_READY_STATUS = 'Ready to extract the active tab.';
export const UNAUTHENTICATED_STATUS = 'Sign in to extract job pages from the active tab.';
export const DRAFT_SAVE_DELAY_MS = 250;
export const DEFAULT_AUTH_STATUS =
  'Request a one-time email code so you never type your password into the extension.';
export const EMAIL_SIGN_IN_CODE_PATTERN = /^\d{6,8}$/;
