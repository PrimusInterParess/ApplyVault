import type { ScrapeResult } from '../shared/models/scrapeResult';
import { POPUP_DRAFT_STORAGE_KEY, type PopupDraft } from './popupTypes';

function isPopupDraft(value: unknown): value is PopupDraft {
  if (!value || typeof value !== 'object') {
    return false;
  }

  const candidate = value as Partial<PopupDraft>;
  const result = candidate.result as Partial<ScrapeResult> | undefined;

  return (
    (candidate.mode === 'scrape' || candidate.mode === 'save') &&
    typeof candidate.statusMessage === 'string' &&
    !!result &&
    typeof result.url === 'string' &&
    typeof result.title === 'string' &&
    typeof result.text === 'string'
  );
}

export function loadPopupDraft(): Promise<PopupDraft | null> {
  return new Promise((resolve) => {
    chrome.storage.local.get([POPUP_DRAFT_STORAGE_KEY], (items) => {
      const runtimeError = chrome.runtime.lastError;

      if (runtimeError) {
        resolve(null);
        return;
      }

      const draft = items[POPUP_DRAFT_STORAGE_KEY];
      resolve(isPopupDraft(draft) ? draft : null);
    });
  });
}

export function savePopupDraft(draft: PopupDraft): Promise<void> {
  return new Promise((resolve) => {
    chrome.storage.local.set({ [POPUP_DRAFT_STORAGE_KEY]: draft }, () => {
      resolve();
    });
  });
}

export function clearPopupDraft(): Promise<void> {
  return new Promise((resolve) => {
    chrome.storage.local.remove(POPUP_DRAFT_STORAGE_KEY, () => {
      resolve();
    });
  });
}
