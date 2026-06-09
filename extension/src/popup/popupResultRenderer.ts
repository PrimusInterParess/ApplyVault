import type { HiringManagerContact, JobDetails } from '../shared/models/scrapeResult';
import { formatPageType, setFieldValue } from './popupViewModel';
import type { PopupDetailsElements, PopupMode, PopupResultPanels } from './popupTypes';

function formatContact(contact: HiringManagerContact): string {
  const contactLabel = contact.label ? `${contact.label}: ` : '';
  return `${contactLabel}${contact.type} - ${contact.value}`;
}

export function renderContacts(contactList: HTMLTextAreaElement, contacts: HiringManagerContact[]): void {
  if (contacts.length === 0) {
    contactList.value = '';
    contactList.placeholder = 'No contact details found.';
    return;
  }

  contactList.value = contacts.map(formatContact).join('\n');
  contactList.placeholder = 'No contact details found.';
}

export function resetStructuredDetails(details: PopupDetailsElements): void {
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

export function populateStructuredDetails(
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

  if (jobDetails.fieldSources) {
    const titleHint = jobDetails.fieldSources.jobTitle;
    if (titleHint) {
      details.jobTitle.title = `source: ${titleHint}`;
    }
  }
}

export function setButtonMode(button: HTMLButtonElement, mode: PopupMode): void {
  button.textContent = mode === 'scrape' ? 'Extract current page' : 'Save';
}

export function setResultPanelsVisibility(panels: PopupResultPanels, isVisible: boolean): void {
  const method = isVisible ? 'removeAttribute' : 'setAttribute';

  panels.details[method]('hidden', '');
  panels.contacts[method]('hidden', '');
  panels.description[method]('hidden', '');
  panels.text[method]('hidden', '');
}

export function clearRenderedResult(
  textArea: HTMLTextAreaElement,
  descriptionArea: HTMLTextAreaElement,
  details: PopupDetailsElements
): void {
  textArea.value = '';
  descriptionArea.value = '';
  resetStructuredDetails(details);
}

export function syncActionButtons(
  primaryButton: HTMLButtonElement,
  renewButton: HTMLButtonElement,
  popupMode: PopupMode,
  isPending: boolean,
  isAuthenticated: boolean
): void {
  primaryButton.disabled = isPending || !isAuthenticated;
  renewButton.disabled = isPending || popupMode === 'scrape' || !isAuthenticated;
}

export function setPendingState(
  primaryButton: HTMLButtonElement,
  renewButton: HTMLButtonElement,
  status: HTMLElement,
  popupMode: PopupMode,
  pendingMessage: string,
  isAuthenticated: boolean
): void {
  syncActionButtons(primaryButton, renewButton, popupMode, true, isAuthenticated);
  status.textContent = pendingMessage;
}
