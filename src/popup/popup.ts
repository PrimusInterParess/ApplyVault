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
import {
  getAuthState,
  requestSignInCode,
  verifySignInCode,
  signOut,
  type ExtensionAuthState
} from '../infrastructure/auth/supabaseAuth';
import { evaluateScrapeResult } from '../shared/utils/scrapeQuality';

type PopupMode = 'scrape' | 'save';
type EditableControl = HTMLInputElement | HTMLTextAreaElement;
type AuthStatusTone = 'default' | 'error';

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
const DEFAULT_AUTH_STATUS = 'Request a one-time email code so you never type your password into the extension.';
const EMAIL_SIGN_IN_CODE_PATTERN = /^\d{6,8}$/;

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
  isPending: boolean,
  canSave: boolean
): void {
  primaryButton.disabled = isPending || (popupMode === 'save' && !canSave);
  renewButton.disabled = isPending || popupMode === 'scrape';
}

function setPendingState(
  primaryButton: HTMLButtonElement,
  renewButton: HTMLButtonElement,
  status: HTMLElement,
  popupMode: PopupMode,
  pendingMessage: string,
  canSave: boolean
): void {
  syncActionButtons(primaryButton, renewButton, popupMode, true, canSave);
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
  const authEmailElement = document.getElementById('auth-email');
  const authCodeElement = document.getElementById('auth-code');
  const authStatusElement = document.getElementById('auth-status');
  const authConnectCopyElement = document.getElementById('auth-connect-copy');
  const authFormElement = document.getElementById('auth-form');
  const authUserElement = document.getElementById('auth-user');
  const authUserEmailElement = document.getElementById('auth-user-email');
  const sendCodeButtonElement = document.getElementById('send-code-button');
  const verifyCodeButtonElement = document.getElementById('verify-code-button');
  const signOutButtonElement = document.getElementById('sign-out-button');
  const buttonElement = document.getElementById('scrape-button');
  const renewButtonElement = document.getElementById('renew-button');
  const statusElementNode = document.getElementById('status');
  const textAreaElement = document.getElementById('scraped-text');
  const descriptionAreaElement = document.getElementById('job-description');
  const scrapedAtElement = document.getElementById('scraped-at');
  const sourceHostnameElement = document.getElementById('source-hostname');
  const pageTypeElement = document.getElementById('page-type');
  const jobTitleElement = document.getElementById('job-title');
  const companyNameElement = document.getElementById('company-name');
  const jobLocationElement = document.getElementById('job-location');
  const hiringManagerElement = document.getElementById('hiring-manager');
  const positionSummaryElement = document.getElementById('position-summary');
  const contactListElement = document.getElementById('hiring-manager-contacts');

  if (
    !(authEmailElement instanceof HTMLInputElement) ||
    !(authCodeElement instanceof HTMLInputElement) ||
    !authStatusElement ||
    !authConnectCopyElement ||
    !authFormElement ||
    !authUserElement ||
    !authUserEmailElement ||
    !(sendCodeButtonElement instanceof HTMLButtonElement) ||
    !(verifyCodeButtonElement instanceof HTMLButtonElement) ||
    !(signOutButtonElement instanceof HTMLButtonElement) ||
    !(buttonElement instanceof HTMLButtonElement) ||
    !(renewButtonElement instanceof HTMLButtonElement) ||
    !statusElementNode ||
    !(textAreaElement instanceof HTMLTextAreaElement) ||
    !(descriptionAreaElement instanceof HTMLTextAreaElement) ||
    !(scrapedAtElement instanceof HTMLInputElement) ||
    !(sourceHostnameElement instanceof HTMLInputElement) ||
    !(pageTypeElement instanceof HTMLInputElement) ||
    !(jobTitleElement instanceof HTMLInputElement) ||
    !(companyNameElement instanceof HTMLInputElement) ||
    !(jobLocationElement instanceof HTMLInputElement) ||
    !(hiringManagerElement instanceof HTMLInputElement) ||
    !(positionSummaryElement instanceof HTMLTextAreaElement) ||
    !(contactListElement instanceof HTMLTextAreaElement)
  ) {
    throw new Error('Popup UI elements are missing.');
  }

  const authEmail = authEmailElement;
  const authCode = authCodeElement;
  const authStatus = authStatusElement;
  const authConnectCopy = authConnectCopyElement;
  const authForm = authFormElement;
  const authUser = authUserElement;
  const authUserEmail = authUserEmailElement;
  const sendCodeButton = sendCodeButtonElement;
  const verifyCodeButton = verifyCodeButtonElement;
  const signOutButton = signOutButtonElement;
  const primaryButton = buttonElement;
  const renewScrapeButton = renewButtonElement;
  const statusElement = statusElementNode;
  const scrapedTextArea = textAreaElement;
  const jobDescriptionArea = descriptionAreaElement;

  const details: PopupDetailsElements = {
    scrapedAt: scrapedAtElement,
    sourceHostname: sourceHostnameElement,
    pageType: pageTypeElement,
    jobTitle: jobTitleElement,
    companyName: companyNameElement,
    jobLocation: jobLocationElement,
    hiringManager: hiringManagerElement,
    positionSummary: positionSummaryElement,
    contacts: contactListElement
  };

  let popupMode: PopupMode = 'scrape';
  let lastScrapeResult: ScrapeResult | null = null;
  let draftSaveTimeoutId: number | null = null;
  let workflowStatusMessage = DEFAULT_READY_STATUS;
  let authState: ExtensionAuthState = {
    session: null,
    currentUser: null,
    apiError: null
  };
  let authPending = false;
  let authFeedbackMessage: string | null = null;
  let authFeedbackTone: AuthStatusTone = 'default';

  function canSaveToApi(): boolean {
    return authState.currentUser !== null && authState.apiError === null;
  }

  function setWorkflowStatus(message: string): void {
    workflowStatusMessage = message;
    statusElement.textContent =
      popupMode === 'save' && !canSaveToApi()
        ? `${message} Sign in to save the extracted record to ApplyVault.`
        : message;
  }

  function setAuthStatus(message: string | null, tone: AuthStatusTone = 'default'): void {
    if (!message) {
      authStatus.textContent = '';
      authStatus.setAttribute('hidden', '');
      authStatus.classList.remove('popup__status--error');
      return;
    }

    authStatus.textContent = message;
    authStatus.removeAttribute('hidden');
    authStatus.classList.toggle('popup__status--error', tone === 'error');
  }

  function renderAuthState(): void {
    const sessionUser = authState.session?.user;
    const email = authState.currentUser?.email || sessionUser?.email || 'No email returned by Supabase.';

    authEmail.disabled = authPending || authState.session !== null;
    authCode.disabled = authPending || authState.session !== null;
    sendCodeButton.disabled = authPending || authState.session !== null;
    verifyCodeButton.disabled = authPending || authState.session !== null;
    signOutButton.disabled = authPending || authState.session === null;

    if (authState.session) {
      authConnectCopy.setAttribute('hidden', '');
      authForm.setAttribute('hidden', '');
      authUser.removeAttribute('hidden');
      signOutButton.removeAttribute('hidden');
      authUserEmail.textContent = email;

      setAuthStatus(
        authFeedbackMessage ?? authState.apiError,
        authState.apiError ? 'error' : authFeedbackTone
      );
    } else {
      authConnectCopy.removeAttribute('hidden');
      authForm.removeAttribute('hidden');
      authUser.setAttribute('hidden', '');
      signOutButton.setAttribute('hidden', '');
      setAuthStatus(authFeedbackMessage ?? DEFAULT_AUTH_STATUS, authFeedbackTone);
    }
  }

  function renderPopupState(isBusy = false): void {
    renderAuthState();
    syncActionButtons(primaryButton, renewScrapeButton, popupMode, isBusy || authPending, canSaveToApi());
    setWorkflowStatus(workflowStatusMessage);
  }

  async function refreshAuthState(): Promise<void> {
    authState = await getAuthState();
    renderPopupState();
  }

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
    renderPopupState();
    setWorkflowStatus(statusMessage);
  }

  function renderResult(result: ScrapeResult, statusMessage: string): void {
    popupMode = 'save';
    lastScrapeResult = result;
    scrapedTextArea.value = result.text;
    jobDescriptionArea.value = result.jobDetails.jobDescription ?? '';
    populateStructuredDetails(details, result.jobDetails, result.extractedAt);
    setButtonMode(primaryButton, popupMode);
    renderPopupState();
    setWorkflowStatus(statusMessage);
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
      'Extracting visible text from the current page...',
      canSaveToApi()
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
        renderPopupState();
        setWorkflowStatus(response.error);
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
      renderPopupState();
      setWorkflowStatus(errorMessage);
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
  resetStructuredDetails(details);
  renderPopupState();
  setWorkflowStatus(DEFAULT_READY_STATUS);

  void (async () => {
    try {
      await refreshAuthState();
    } catch (error) {
      authFeedbackMessage =
        error instanceof Error ? error.message : 'Loading the saved authentication state failed.';
      authFeedbackTone = 'error';
      renderPopupState();
    }

    await restoreDraftIfAvailable();
  })();

  async function runRequestSignInCode(): Promise<void> {
    const email = authEmail.value.trim();

    if (!email) {
      authFeedbackMessage = 'Enter your email address to receive a sign-in code.';
      authFeedbackTone = 'error';
      renderPopupState();
      return;
    }

    authPending = true;
    authFeedbackMessage = 'Sending a one-time sign-in code to your email...';
    authFeedbackTone = 'default';
    renderPopupState();

    try {
      await requestSignInCode(email);
      authCode.focus();
      authFeedbackMessage =
        'Check your email for the ApplyVault sign-in code, then enter it below. If you only received a magic link, update the Supabase Magic Link email template to include {{ .Token }}.';
      authFeedbackTone = 'default';
    } catch (error) {
      authFeedbackMessage = error instanceof Error ? error.message : 'Sending the sign-in code failed.';
      authFeedbackTone = 'error';
    } finally {
      authPending = false;
      renderPopupState();
    }
  }

  async function runVerifySignInCode(): Promise<void> {
    const email = authEmail.value.trim();
    const code = authCode.value.trim();

    if (!email || !code) {
      authFeedbackMessage = 'Enter both your email address and the sign-in code from your email.';
      authFeedbackTone = 'error';
      renderPopupState();
      return;
    }

    if (!EMAIL_SIGN_IN_CODE_PATTERN.test(code)) {
      authFeedbackMessage = 'Enter the numeric sign-in code from your email.';
      authFeedbackTone = 'error';
      renderPopupState();
      return;
    }

    authPending = true;
    authFeedbackMessage = 'Verifying your sign-in code...';
    authFeedbackTone = 'default';
    renderPopupState();

    try {
      await verifySignInCode(email, code);
      authCode.value = '';
      authFeedbackMessage = null;
      authFeedbackTone = 'default';
      await refreshAuthState();

      if (popupMode === 'save' && lastScrapeResult) {
        setWorkflowStatus('Signed in. You can now save the extracted record to ApplyVault.');
      }
    } catch (error) {
      authCode.value = '';
      authFeedbackMessage = error instanceof Error ? error.message : 'Verifying the sign-in code failed.';
      authFeedbackTone = 'error';
    } finally {
      authPending = false;
      renderPopupState();
    }
  }

  async function runSignOut(): Promise<void> {
    authPending = true;
    authFeedbackMessage = 'Signing out from ApplyVault...';
    authFeedbackTone = 'default';
    renderPopupState();

    try {
      await signOut();
      authCode.value = '';
      authFeedbackMessage = null;
      authFeedbackTone = 'default';
      await refreshAuthState();

      if (popupMode === 'save' && lastScrapeResult) {
        setWorkflowStatus('Signed out. Sign in again to save the extracted record to ApplyVault.');
      }
    } catch (error) {
      authFeedbackMessage = error instanceof Error ? error.message : 'Sign out failed.';
      authFeedbackTone = 'error';
    } finally {
      authPending = false;
      renderPopupState();
    }
  }

  sendCodeButton.addEventListener('click', () => {
    void runRequestSignInCode();
  });

  verifyCodeButton.addEventListener('click', () => {
    void runVerifySignInCode();
  });

  signOutButton.addEventListener('click', () => {
    void runSignOut();
  });

  for (const input of [authEmail, authCode]) {
    input.addEventListener('input', () => {
      if (!authPending && authFeedbackMessage) {
        authFeedbackMessage = null;
        authFeedbackTone = 'default';
        renderPopupState();
      }
    });

    input.addEventListener('keydown', (event) => {
      if (event.key !== 'Enter' || authState.session) {
        return;
      }

      event.preventDefault();
      void (authCode.value.trim() ? runVerifySignInCode() : runRequestSignInCode());
    });
  }

  authCode.addEventListener('input', () => {
    const sanitizedCode = authCode.value.replace(/\D/g, '').slice(0, 8);

    if (authCode.value !== sanitizedCode) {
      authCode.value = sanitizedCode;
    }
  });

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

    if (!canSaveToApi()) {
      renderPopupState();
      setWorkflowStatus('Sign in to save the extracted record to ApplyVault.');
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
      'Saving extracted data to the ApplyVault API...',
      canSaveToApi()
    );

    try {
      const response = await sendSaveRequest(draft.result);

      if (!response.success) {
        renderPopupState();
        setWorkflowStatus(response.error);
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

      renderPopupState();
      setWorkflowStatus(errorMessage);
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
