import {
  MessageType,
  type SaveScrapeResultRequest,
  type SaveScrapeResultResponse,
  type ScrapeActiveTabRequest,
  type ScrapeActiveTabResponse
} from '../shared/contracts/messages';
import type { HiringManagerContact, JobDetails, ScrapeResult } from '../shared/models/scrapeResult';

type PopupMode = 'scrape' | 'save';
type EditableControl = HTMLInputElement | HTMLTextAreaElement;

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

function setPendingState(
  button: HTMLButtonElement,
  status: HTMLElement,
  textArea: HTMLTextAreaElement,
  descriptionArea: HTMLTextAreaElement,
  details: PopupDetailsElements,
  isPending: boolean
): void {
  button.disabled = isPending;
  status.textContent = isPending ? 'Scraping visible text from the current page...' : status.textContent;

  if (isPending) {
    textArea.value = '';
    descriptionArea.value = '';
    resetStructuredDetails(details);
  }
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

function setButtonMode(button: HTMLButtonElement, mode: PopupMode): void {
  button.textContent = mode === 'scrape' ? 'Scrape current page' : 'Save to API';
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
  setFieldValue(details.scrapedAt, undefined, 'Not scraped yet.');
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
  setFieldValue(details.scrapedAt, extractedAt, 'Not scraped yet.');
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

  return {
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
}

document.addEventListener('DOMContentLoaded', () => {
  const button = document.getElementById('scrape-button');
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

  setButtonMode(button, popupMode);

  button.addEventListener('click', async () => {
    if (popupMode === 'scrape') {
      setPendingState(button, status, textArea, descriptionArea, details, true);
      lastScrapeResult = null;

      try {
        const response = await sendScrapeRequest();

        if (!response.success) {
          status.textContent = response.error;
          return;
        }

        lastScrapeResult = response.data;
        textArea.value = response.data.text;
        descriptionArea.value = response.data.jobDetails.jobDescription ?? '';
        populateStructuredDetails(details, response.data.jobDetails, response.data.extractedAt);

        popupMode = 'save';
        setButtonMode(button, popupMode);
        status.textContent = `Scraped ${response.data.textLength} characters from ${response.data.jobDetails.jobTitle ?? response.data.title}. Review the data, then save it to the API.`;
      } catch (error) {
        status.textContent = error instanceof Error ? error.message : 'The scrape request failed.';
      } finally {
        button.disabled = false;
      }

      return;
    }

    if (!lastScrapeResult) {
      popupMode = 'scrape';
      setButtonMode(button, popupMode);
      status.textContent = 'Scrape a page before saving.';
      return;
    }

    button.disabled = true;
    status.textContent = 'Saving scraped data to the ASP.NET API...';

    try {
      const payload = buildScrapeResultForSave(lastScrapeResult, details, textArea, descriptionArea);
      const response = await sendSaveRequest(payload);

      if (!response.success) {
        status.textContent = response.error;
        return;
      }

      lastScrapeResult = payload;
      popupMode = 'scrape';
      setButtonMode(button, popupMode);
      status.textContent = `Saved to the ASP.NET API at ${response.data.savedAt}. Record id: ${response.data.id}.`;
    } catch (error) {
      status.textContent = error instanceof Error ? error.message : 'Saving the scrape result failed.';
    } finally {
      button.disabled = false;
    }
  });
});
