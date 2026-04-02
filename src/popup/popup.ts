import {
  MessageType,
  type SaveScrapeResultRequest,
  type SaveScrapeResultResponse,
  type ScrapeActiveTabRequest,
  type ScrapeActiveTabResponse
} from '../shared/contracts/messages';
import type {
  ExtractionIssue,
  HiringManagerContact,
  JobDetails,
  ScrapeResult
} from '../shared/models/scrapeResult';
import { evaluateScrapeResult } from '../shared/utils/scrapeQuality';

type PopupMode = 'scrape' | 'save';
type EditableControl = HTMLInputElement | HTMLTextAreaElement;

interface PopupDraft {
  mode: PopupMode;
  result: ScrapeResult;
  statusMessage: string;
}

interface PopupDetailsElements {
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

const POPUP_DRAFT_STORAGE_KEY = 'popupDraft';
const DEFAULT_READY_STATUS = 'Ready to extract the active tab.';
const DRAFT_SAVE_DELAY_MS = 250;

function sendScrapeRequest(): Promise<ScrapeActiveTabResponse> {
  const request: ScrapeActiveTabRequest = {
    type: MessageType.ScrapeActiveTab
  };

  return new Promise((resolve) => {
    chrome.runtime.sendMessage(request, (response?: ScrapeActiveTabResponse) => {
      const runtimeError = chrome.runtime.lastError;

      if (runtimeError) {
        resolve({
          success: false,
          error: runtimeError.message || 'Chrome could not contact the background service worker.'
        });
        return;
      }

      if (!response) {
        resolve({
          success: false,
          error: 'The background service worker did not return a response.'
        });
        return;
      }

      resolve(response);
    });
  });
}

function sendSaveRequest(payload: ScrapeResult): Promise<SaveScrapeResultResponse> {
  const request: SaveScrapeResultRequest = {
    type: MessageType.SaveScrapeResult,
    payload
  };

  return new Promise((resolve) => {
    chrome.runtime.sendMessage(request, (response?: SaveScrapeResultResponse) => {
      const runtimeError = chrome.runtime.lastError;

      if (runtimeError) {
        resolve({
          success: false,
          error: runtimeError.message || 'Chrome could not contact the background service worker.'
        });
        return;
      }

      if (!response) {
        resolve({
          success: false,
          error: 'The background service worker did not return a response.'
        });
        return;
      }

      resolve(response);
    });
  });
}

function getActiveTabUrl(): Promise<string | undefined> {
  return new Promise((resolve) => {
    chrome.tabs.query({ active: true, currentWindow: true }, (tabs) => {
      resolve(tabs[0]?.url);
    });
  });
}

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

function loadPopupDraft(): Promise<PopupDraft | null> {
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

function savePopupDraft(draft: PopupDraft): Promise<void> {
  return new Promise((resolve) => {
    chrome.storage.local.set({ [POPUP_DRAFT_STORAGE_KEY]: draft }, () => {
      resolve();
    });
  });
}

function clearPopupDraft(): Promise<void> {
  return new Promise((resolve) => {
    chrome.storage.local.remove(POPUP_DRAFT_STORAGE_KEY, () => {
      resolve();
    });
  });
}

function syncActionButtons(
  primaryButton: HTMLButtonElement,
  renewButton: HTMLButtonElement,
  popupMode: PopupMode,
  isPending: boolean
): void {
  primaryButton.disabled = isPending;
  renewButton.disabled = isPending || popupMode === 'scrape';
}

function setPendingState(
  primaryButton: HTMLButtonElement,
  renewButton: HTMLButtonElement,
  status: HTMLElement,
  popupMode: PopupMode,
  pendingMessage: string
): void {
  syncActionButtons(primaryButton, renewButton, popupMode, true);
  status.textContent = pendingMessage;
}

function clearRenderedResult(
  textArea: HTMLTextAreaElement,
  descriptionArea: HTMLTextAreaElement,
  details: PopupDetailsElements
): void {
  textArea.value = '';
  descriptionArea.value = '';
  resetStructuredDetails(details);
}

function setFieldValue(element: EditableControl, value: string | undefined, emptyText: string): void {
  element.value = value?.trim() || '';
  element.placeholder = emptyText;
}

function getOptionalValue(value: string): string | undefined {
  const trimmedValue = value.trim();
  return trimmedValue.length > 0 ? trimmedValue : undefined;
}

function formatPageType(pageType: JobDetails['detectedPageType']): string {
  return pageType
    .split('-')
    .map((part) => `${part.charAt(0).toUpperCase()}${part.slice(1)}`)
    .join(' ');
}

function parsePageType(
  value: string,
  fallbackPageType: JobDetails['detectedPageType']
): JobDetails['detectedPageType'] {
  const normalizedValue = value.trim().toLowerCase().replace(/\s+/g, '-');

  if (
    normalizedValue === 'linkedin-job' ||
    normalizedValue === 'job-posting' ||
    normalizedValue === 'generic-page'
  ) {
    return normalizedValue;
  }

  return fallbackPageType;
}

function formatContact(contact: HiringManagerContact): string {
  const contactLabel = contact.label ? `${contact.label}: ` : '';
  return `${contactLabel}${contact.type} - ${contact.value}`;
}

function parseContactLine(line: string): HiringManagerContact | null {
  const match = /^(?:(.+?):\s*)?(email|phone|linkedin|url)\s*-\s*(.+)$/i.exec(line.trim());

  if (!match) {
    return null;
  }

  const [, label, type, value] = match;

  return {
    type: type.toLowerCase() as HiringManagerContact['type'],
    value: value.trim(),
    label: label?.trim() || undefined
  };
}

function parseContacts(value: string, fallbackContacts: HiringManagerContact[]): HiringManagerContact[] {
  const lines = value
    .split('\n')
    .map((line) => line.trim())
    .filter((line) => line.length > 0);

  if (lines.length === 0) {
    return [];
  }

  const parsedContacts = lines.map(parseContactLine);

  if (parsedContacts.some((contact) => contact === null)) {
    return fallbackContacts;
  }

  return parsedContacts as HiringManagerContact[];
}

function formatIssueSummary(issues: ExtractionIssue[]): string {
  return issues.slice(0, 3).map((issue) => issue.message).join(' ');
}

function buildScrapeStatusMessage(result: ScrapeResult): string {
  const targetLabel = result.jobDetails.jobTitle ?? result.title;
  const attempts = result.extraction?.attempts ?? 1;
  const attemptLabel = attempts === 1 ? '1 attempt' : `${attempts} attempts`;

  if (!result.extraction || result.extraction.status === 'valid') {
    return `Captured ${result.textLength} characters from ${targetLabel} after ${attemptLabel}. Review the data, then save it to the API.`;
  }

  const issueSummary = formatIssueSummary(result.extraction.issues);
  const statusLabel =
    result.extraction.status === 'partial' ? 'Partial extraction' : 'Low-confidence extraction';

  return `${statusLabel} after ${attemptLabel}. ${issueSummary} Review the extracted fields before saving.`;
}

function setButtonMode(button: HTMLButtonElement, mode: PopupMode): void {
  button.textContent = mode === 'scrape' ? 'Extract current page' : 'Save';
}

function renderContacts(contactList: HTMLTextAreaElement, contacts: HiringManagerContact[]): void {
  if (contacts.length === 0) {
    contactList.value = '';
    contactList.placeholder = 'No contact details found.';
    return;
  }

  contactList.value = contacts.map(formatContact).join('\n');
  contactList.placeholder = 'No contact details found.';
}

function resetStructuredDetails(details: PopupDetailsElements): void {
  setFieldValue(details.scrapedAt, undefined, 'Not captured yet.');
  setFieldValue(details.sourceHostname, undefined, 'Not available.');
  setFieldValue(details.pageType, undefined, 'Not available.');
  setFieldValue(details.jobTitle, undefined, 'Not found.');
  setFieldValue(details.companyName, undefined, 'Not found.');
  setFieldValue(details.jobLocation, undefined, 'Not found.');
  setFieldValue(details.hiringManager, undefined, 'Not found.');
  setFieldValue(details.positionSummary, undefined, 'Not found.');
  renderContacts(details.contacts, []);
}

function populateStructuredDetails(
  details: PopupDetailsElements,
  jobDetails: JobDetails,
  extractedAt: string
): void {
  setFieldValue(details.scrapedAt, extractedAt, 'Not captured yet.');
  setFieldValue(details.sourceHostname, jobDetails.sourceHostname, 'Not available.');
  setFieldValue(details.pageType, formatPageType(jobDetails.detectedPageType), 'Not available.');
  setFieldValue(details.jobTitle, jobDetails.jobTitle, 'Not found.');
  setFieldValue(details.companyName, jobDetails.companyName, 'Not found.');
  setFieldValue(details.jobLocation, jobDetails.location, 'Not found.');
  setFieldValue(details.hiringManager, jobDetails.hiringManagerName, 'Not found.');
  setFieldValue(details.positionSummary, jobDetails.positionSummary, 'Not found.');
  renderContacts(details.contacts, jobDetails.hiringManagerContacts);
}

function buildScrapeResultForSave(
  originalResult: ScrapeResult,
  details: PopupDetailsElements,
  textArea: HTMLTextAreaElement,
  descriptionArea: HTMLTextAreaElement
): ScrapeResult {
  const text = textArea.value;
  const updatedResult: ScrapeResult = {
    ...originalResult,
    text,
    textLength: text.length,
    extractedAt: getOptionalValue(details.scrapedAt.value) ?? originalResult.extractedAt,
    jobDetails: {
      ...originalResult.jobDetails,
      sourceHostname:
        getOptionalValue(details.sourceHostname.value) ?? originalResult.jobDetails.sourceHostname,
      detectedPageType: parsePageType(
        details.pageType.value,
        originalResult.jobDetails.detectedPageType
      ),
      jobTitle: getOptionalValue(details.jobTitle.value),
      companyName: getOptionalValue(details.companyName.value),
      location: getOptionalValue(details.jobLocation.value),
      jobDescription: getOptionalValue(descriptionArea.value),
      positionSummary: getOptionalValue(details.positionSummary.value),
      hiringManagerName: getOptionalValue(details.hiringManager.value),
      hiringManagerContacts: parseContacts(
        details.contacts.value,
        originalResult.jobDetails.hiringManagerContacts
      )
    }
  };

  return {
    ...updatedResult,
    extraction: {
      ...evaluateScrapeResult(updatedResult),
      attempts: originalResult.extraction?.attempts ?? 1
    }
  };
}

document.addEventListener('DOMContentLoaded', () => {
  const button = document.getElementById('scrape-button');
  const renewButton = document.getElementById('renew-button');
  const status = document.getElementById('status');
  const textArea = document.getElementById('scraped-text');
  const descriptionArea = document.getElementById('job-description');
  const scrapedAt = document.getElementById('scraped-at');
  const sourceHostname = document.getElementById('source-hostname');
  const pageType = document.getElementById('page-type');
  const jobTitle = document.getElementById('job-title');
  const companyName = document.getElementById('company-name');
  const jobLocation = document.getElementById('job-location');
  const hiringManager = document.getElementById('hiring-manager');
  const positionSummary = document.getElementById('position-summary');
  const contactList = document.getElementById('hiring-manager-contacts');

  if (
    !(button instanceof HTMLButtonElement) ||
    !(renewButton instanceof HTMLButtonElement) ||
    !status ||
    !(textArea instanceof HTMLTextAreaElement) ||
    !(descriptionArea instanceof HTMLTextAreaElement) ||
    !(scrapedAt instanceof HTMLInputElement) ||
    !(sourceHostname instanceof HTMLInputElement) ||
    !(pageType instanceof HTMLInputElement) ||
    !(jobTitle instanceof HTMLInputElement) ||
    !(companyName instanceof HTMLInputElement) ||
    !(jobLocation instanceof HTMLInputElement) ||
    !(hiringManager instanceof HTMLInputElement) ||
    !(positionSummary instanceof HTMLTextAreaElement) ||
    !(contactList instanceof HTMLTextAreaElement)
  ) {
    throw new Error('Popup UI elements are missing.');
  }

  const primaryButton = button;
  const renewScrapeButton = renewButton;
  const statusElement = status;
  const scrapedTextArea = textArea;
  const jobDescriptionArea = descriptionArea;

  const details: PopupDetailsElements = {
    scrapedAt,
    sourceHostname,
    pageType,
    jobTitle,
    companyName,
    jobLocation,
    hiringManager,
    positionSummary,
    contacts: contactList
  };

  let popupMode: PopupMode = 'scrape';
  let lastScrapeResult: ScrapeResult | null = null;
  let draftSaveTimeoutId: number | null = null;

  function clearDraftSaveTimeout(): void {
    if (draftSaveTimeoutId !== null) {
      window.clearTimeout(draftSaveTimeoutId);
      draftSaveTimeoutId = null;
    }
  }

  function resetPopupState(statusMessage = DEFAULT_READY_STATUS): void {
    popupMode = 'scrape';
    lastScrapeResult = null;
    clearDraftSaveTimeout();
    scrapedTextArea.value = '';
    jobDescriptionArea.value = '';
    resetStructuredDetails(details);
    setButtonMode(primaryButton, popupMode);
    syncActionButtons(primaryButton, renewScrapeButton, popupMode, false);
    statusElement.textContent = statusMessage;
  }

  function renderResult(result: ScrapeResult, statusMessage: string): void {
    popupMode = 'save';
    lastScrapeResult = result;
    scrapedTextArea.value = result.text;
    jobDescriptionArea.value = result.jobDetails.jobDescription ?? '';
    populateStructuredDetails(details, result.jobDetails, result.extractedAt);
    setButtonMode(primaryButton, popupMode);
    syncActionButtons(primaryButton, renewScrapeButton, popupMode, false);
    statusElement.textContent = statusMessage;
  }

  function buildCurrentDraft(): PopupDraft | null {
    if (!lastScrapeResult) {
      return null;
    }

    const updatedResult = buildScrapeResultForSave(
      lastScrapeResult,
      details,
      scrapedTextArea,
      jobDescriptionArea
    );
    lastScrapeResult = updatedResult;

    return {
      mode: 'save',
      result: updatedResult,
      statusMessage: statusElement.textContent?.trim() || DEFAULT_READY_STATUS
    };
  }

  function scheduleDraftSave(): void {
    if (!lastScrapeResult) {
      return;
    }

    clearDraftSaveTimeout();
    draftSaveTimeoutId = window.setTimeout(() => {
      const draft = buildCurrentDraft();

      if (!draft) {
        return;
      }

      void savePopupDraft(draft);
    }, DRAFT_SAVE_DELAY_MS);
  }

  async function restoreDraftIfAvailable(): Promise<void> {
    const [activeTabUrl, savedDraft] = await Promise.all([getActiveTabUrl(), loadPopupDraft()]);

    if (!savedDraft) {
      resetPopupState();
      return;
    }

    if (activeTabUrl && savedDraft.result.url !== activeTabUrl) {
      resetPopupState();
      return;
    }

    const restoredStatus = savedDraft.statusMessage
      ? `Restored saved draft for this page. ${savedDraft.statusMessage}`
      : 'Restored saved draft for this page.';

    renderResult(savedDraft.result, restoredStatus);
  }

  async function persistRenderedResult(result: ScrapeResult, statusMessage: string): Promise<void> {
    renderResult(result, statusMessage);
    await savePopupDraft({
      mode: 'save',
      result,
      statusMessage
    });
  }

  async function runScrape(): Promise<void> {
    clearDraftSaveTimeout();
    const previousDraft = buildCurrentDraft();

    clearRenderedResult(scrapedTextArea, jobDescriptionArea, details);
    setPendingState(
      primaryButton,
      renewScrapeButton,
      statusElement,
      popupMode,
      'Extracting visible text from the current page...'
    );

    try {
      const response = await sendScrapeRequest();

      if (!response.success) {
        if (previousDraft) {
          renderResult(previousDraft.result, response.error);
          await savePopupDraft({
            ...previousDraft,
            statusMessage: response.error
          });
          return;
        }

        popupMode = 'scrape';
        lastScrapeResult = null;
        syncActionButtons(primaryButton, renewScrapeButton, popupMode, false);
        statusElement.textContent = response.error;
        return;
      }

      const statusMessage = buildScrapeStatusMessage(response.data);
      await persistRenderedResult(response.data, statusMessage);
    } catch (error) {
      const errorMessage = error instanceof Error ? error.message : 'The extraction request failed.';

      if (previousDraft) {
        renderResult(previousDraft.result, errorMessage);
        await savePopupDraft({
          ...previousDraft,
          statusMessage: errorMessage
        });
        return;
      }

      popupMode = 'scrape';
      lastScrapeResult = null;
      syncActionButtons(primaryButton, renewScrapeButton, popupMode, false);
      statusElement.textContent = errorMessage;
    }
  }

  const editableControls: EditableControl[] = [
    scrapedTextArea,
    jobDescriptionArea,
    details.scrapedAt,
    details.sourceHostname,
    details.pageType,
    details.jobTitle,
    details.companyName,
    details.jobLocation,
    details.hiringManager,
    details.positionSummary,
    details.contacts
  ];

  for (const control of editableControls) {
    control.addEventListener('input', () => {
      scheduleDraftSave();
    });
  }

  setButtonMode(primaryButton, popupMode);
  syncActionButtons(primaryButton, renewScrapeButton, popupMode, false);
  resetStructuredDetails(details);
  statusElement.textContent = DEFAULT_READY_STATUS;

  void restoreDraftIfAvailable();

  primaryButton.addEventListener('click', async () => {
    if (popupMode === 'scrape') {
      await runScrape();
      return;
    }

    if (!lastScrapeResult) {
      resetPopupState('Extract a page before saving.');
      await clearPopupDraft();
      return;
    }

    clearDraftSaveTimeout();
    const draft = buildCurrentDraft();

    if (!draft) {
      resetPopupState('Extract a page before saving.');
      await clearPopupDraft();
      return;
    }

    popupMode = 'save';
    lastScrapeResult = draft.result;
    setPendingState(
      primaryButton,
      renewScrapeButton,
      statusElement,
      popupMode,
      'Saving extracted data to the ASP.NET API...'
    );

    try {
      const response = await sendSaveRequest(draft.result);

      if (!response.success) {
        syncActionButtons(primaryButton, renewScrapeButton, popupMode, false);
        statusElement.textContent = response.error;
        await savePopupDraft({
          ...draft,
          statusMessage: response.error
        });
        return;
      }

      const successMessage = `Saved to the ASP.NET API at ${response.data.savedAt}. Record id: ${response.data.id}.`;
      resetPopupState(`${successMessage} Ready to extract the current page.`);
      await clearPopupDraft();
    } catch (error) {
      const errorMessage =
        error instanceof Error ? error.message : 'Saving the extracted result failed.';

      syncActionButtons(primaryButton, renewScrapeButton, popupMode, false);
      statusElement.textContent = errorMessage;
      await savePopupDraft({
        ...draft,
        statusMessage: errorMessage
      });
    }
  });

  renewScrapeButton.addEventListener('click', async () => {
    resetPopupState('Draft cleared. Extract the current page when you are ready.');
    await clearPopupDraft();
  });

  window.addEventListener('beforeunload', () => {
    clearDraftSaveTimeout();

    const draft = buildCurrentDraft();

    if (!draft) {
      return;
    }

    void savePopupDraft(draft);
  });
});
