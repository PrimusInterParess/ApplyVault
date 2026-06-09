import {
  getAuthState,
  requestSignInCode,
  signOut,
  verifySignInCode,
  type ExtensionAuthState
} from '../infrastructure/auth/supabaseAuth';
import type { ScrapeResult } from '../shared/models/scrapeResult';
import { clearPopupDraft, loadPopupDraft, savePopupDraft } from './popupDraftStorage';
import {
  clearRenderedResult,
  populateStructuredDetails,
  resetStructuredDetails,
  setButtonMode,
  setPendingState,
  setResultPanelsVisibility,
  syncActionButtons
} from './popupResultRenderer';
import { getActiveTabUrl, sendSaveRequest, sendScrapeRequest } from './popupScrapeClient';
import {
  DEFAULT_AUTH_STATUS,
  DEFAULT_READY_STATUS,
  DRAFT_SAVE_DELAY_MS,
  EMAIL_SIGN_IN_CODE_PATTERN,
  UNAUTHENTICATED_STATUS,
  type AuthStatusTone,
  type EditableControl,
  type PopupDetailsElements,
  type PopupDraft,
  type PopupMode,
  type PopupResultPanels
} from './popupTypes';
import { buildScrapeResultForSave, buildScrapeStatusMessage } from './popupViewModel';

export function initPopup(): void {
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
  const workflowPanelElement = document.getElementById('workflow-panel');
  const resultDetailsPanelElement = document.getElementById('results-details-panel');
  const resultContactsPanelElement = document.getElementById('results-contacts-panel');
  const resultDescriptionPanelElement = document.getElementById('results-description-panel');
  const resultTextPanelElement = document.getElementById('results-text-panel');
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
    !workflowPanelElement ||
    !(sendCodeButtonElement instanceof HTMLButtonElement) ||
    !(verifyCodeButtonElement instanceof HTMLButtonElement) ||
    !(signOutButtonElement instanceof HTMLButtonElement) ||
    !(buttonElement instanceof HTMLButtonElement) ||
    !(renewButtonElement instanceof HTMLButtonElement) ||
    !statusElementNode ||
    !(textAreaElement instanceof HTMLTextAreaElement) ||
    !(descriptionAreaElement instanceof HTMLTextAreaElement) ||
    !resultDetailsPanelElement ||
    !resultContactsPanelElement ||
    !resultDescriptionPanelElement ||
    !resultTextPanelElement ||
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

  const primaryButton = buttonElement;
  const renewScrapeButton = renewButtonElement;
  const statusElement = statusElementNode;
  const scrapedTextArea = textAreaElement;
  const jobDescriptionArea = descriptionAreaElement;
  const workflowPanel = workflowPanelElement;
  const resultPanels: PopupResultPanels = {
    details: resultDetailsPanelElement,
    contacts: resultContactsPanelElement,
    description: resultDescriptionPanelElement,
    text: resultTextPanelElement
  };
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
  let authState: ExtensionAuthState = { session: null, currentUser: null, apiError: null };
  let authPending = false;
  let authFeedbackMessage: string | null = null;
  let authFeedbackTone: AuthStatusTone = 'default';

  const isAuthenticated = () => authState.currentUser !== null && authState.apiError === null;

  const setWorkflowStatus = (message: string) => {
    workflowStatusMessage = message;
    statusElement.textContent = message;
  };

  const clearDraftSaveTimeout = () => {
    if (draftSaveTimeoutId !== null) {
      window.clearTimeout(draftSaveTimeoutId);
      draftSaveTimeoutId = null;
    }
  };

  const setAuthStatus = (message: string | null, tone: AuthStatusTone = 'default') => {
    if (!message) {
      authStatusElement.textContent = '';
      authStatusElement.setAttribute('hidden', '');
      authStatusElement.classList.remove('popup__status--error');
      return;
    }

    authStatusElement.textContent = message;
    authStatusElement.removeAttribute('hidden');
    authStatusElement.classList.toggle('popup__status--error', tone === 'error');
  };

  const renderAuthState = () => {
    const sessionUser = authState.session?.user;
    const email = authState.currentUser?.email || sessionUser?.email || 'No email returned by Supabase.';

    authEmailElement.disabled = authPending || authState.session !== null;
    authCodeElement.disabled = authPending || authState.session !== null;
    sendCodeButtonElement.disabled = authPending || authState.session !== null;
    verifyCodeButtonElement.disabled = authPending || authState.session !== null;
    signOutButtonElement.disabled = authPending || authState.session === null;

    if (authState.session) {
      authConnectCopyElement.setAttribute('hidden', '');
      authFormElement.setAttribute('hidden', '');
      authUserElement.removeAttribute('hidden');
      signOutButtonElement.removeAttribute('hidden');
      authUserEmailElement.textContent = email;
      setAuthStatus(authFeedbackMessage ?? authState.apiError, authState.apiError ? 'error' : authFeedbackTone);
    } else {
      authConnectCopyElement.removeAttribute('hidden');
      authFormElement.removeAttribute('hidden');
      authUserElement.setAttribute('hidden', '');
      signOutButtonElement.setAttribute('hidden', '');
      setAuthStatus(authFeedbackMessage ?? DEFAULT_AUTH_STATUS, authFeedbackTone);
    }
  };

  const renderWorkflowAccess = () => {
    if (isAuthenticated()) {
      workflowPanel.removeAttribute('hidden');
      return;
    }

    workflowPanel.setAttribute('hidden', '');

    if (lastScrapeResult !== null || popupMode === 'save') {
      clearExtractionState(UNAUTHENTICATED_STATUS);
    }
  };

  const renderPopupState = (isBusy = false) => {
    renderAuthState();
    renderWorkflowAccess();
    syncActionButtons(primaryButton, renewScrapeButton, popupMode, isBusy || authPending, isAuthenticated());
    setWorkflowStatus(isAuthenticated() ? workflowStatusMessage : UNAUTHENTICATED_STATUS);
  };

  const clearExtractionState = (statusMessage = DEFAULT_READY_STATUS) => {
    popupMode = 'scrape';
    lastScrapeResult = null;
    clearDraftSaveTimeout();
    clearRenderedResult(scrapedTextArea, jobDescriptionArea, details);
    setResultPanelsVisibility(resultPanels, false);
    setButtonMode(primaryButton, popupMode);
    setWorkflowStatus(statusMessage);
  };

  const resetPopupState = (statusMessage = DEFAULT_READY_STATUS) => {
    clearExtractionState(statusMessage);
    renderPopupState();
  };

  const renderResult = (result: ScrapeResult, statusMessage: string) => {
    popupMode = 'save';
    lastScrapeResult = result;
    scrapedTextArea.value = result.text;
    jobDescriptionArea.value = result.jobDetails.jobDescription ?? '';
    populateStructuredDetails(details, result.jobDetails, result.extractedAt);
    setResultPanelsVisibility(resultPanels, true);
    setButtonMode(primaryButton, popupMode);
    renderPopupState();
    setWorkflowStatus(statusMessage);
  };

  const buildCurrentDraft = (): PopupDraft | null => {
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
  };

  const scheduleDraftSave = () => {
    if (!lastScrapeResult) {
      return;
    }

    clearDraftSaveTimeout();
    draftSaveTimeoutId = window.setTimeout(() => {
      const draft = buildCurrentDraft();
      if (draft) {
        void savePopupDraft(draft);
      }
    }, DRAFT_SAVE_DELAY_MS);
  };

  const restoreDraftIfAvailable = async () => {
    if (!isAuthenticated()) {
      resetPopupState(UNAUTHENTICATED_STATUS);
      return;
    }

    const [activeTabUrl, savedDraft] = await Promise.all([getActiveTabUrl(), loadPopupDraft()]);

    if (!savedDraft) {
      resetPopupState();
      return;
    }

    if (activeTabUrl && savedDraft.result.url !== activeTabUrl) {
      resetPopupState();
      return;
    }

    renderResult(
      savedDraft.result,
      savedDraft.statusMessage
        ? `Restored saved draft for this page. ${savedDraft.statusMessage}`
        : 'Restored saved draft for this page.'
    );
  };

  const runScrape = async () => {
    if (!isAuthenticated()) {
      renderPopupState();
      setWorkflowStatus(UNAUTHENTICATED_STATUS);
      return;
    }

    clearDraftSaveTimeout();
    const previousDraft = buildCurrentDraft();
    clearRenderedResult(scrapedTextArea, jobDescriptionArea, details);
    setPendingState(
      primaryButton,
      renewScrapeButton,
      statusElement,
      popupMode,
      'Extracting visible text from the current page...',
      isAuthenticated()
    );

    try {
      const response = await sendScrapeRequest();

      if (!response.success) {
        if (previousDraft) {
          renderResult(previousDraft.result, response.error);
          await savePopupDraft({ ...previousDraft, statusMessage: response.error });
          return;
        }

        popupMode = 'scrape';
        lastScrapeResult = null;
        renderPopupState();
        setWorkflowStatus(response.error);
        return;
      }

      const statusMessage = buildScrapeStatusMessage(response.data);
      renderResult(response.data, statusMessage);
      await savePopupDraft({ mode: 'save', result: response.data, statusMessage });
    } catch (error) {
      const errorMessage = error instanceof Error ? error.message : 'The extraction request failed.';

      if (previousDraft) {
        renderResult(previousDraft.result, errorMessage);
        await savePopupDraft({ ...previousDraft, statusMessage: errorMessage });
        return;
      }

      popupMode = 'scrape';
      lastScrapeResult = null;
      renderPopupState();
      setWorkflowStatus(errorMessage);
    }
  };

  const refreshAuthState = async () => {
    authState = await getAuthState();
    renderPopupState();
  };

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

  sendCodeButtonElement.addEventListener('click', () => {
    void (async () => {
      const email = authEmailElement.value.trim();
      if (!email) {
        authFeedbackMessage = 'Enter your email address to receive a sign-in code.';
        authFeedbackTone = 'error';
        renderPopupState();
        return;
      }

      authPending = true;
      authFeedbackMessage = 'Sending a one-time sign-in code to your email...';
      renderPopupState();

      try {
        await requestSignInCode(email);
        authCodeElement.focus();
        authFeedbackMessage =
          'Check your email for the ApplyVault sign-in code, then enter it below. If you only received a magic link, update the Supabase Magic Link email template to include {{ .Token }}.';
      } catch (error) {
        authFeedbackMessage = error instanceof Error ? error.message : 'Sending the sign-in code failed.';
        authFeedbackTone = 'error';
      } finally {
        authPending = false;
        renderPopupState();
      }
    })();
  });

  verifyCodeButtonElement.addEventListener('click', () => {
    void (async () => {
      const email = authEmailElement.value.trim();
      const code = authCodeElement.value.trim();

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
      renderPopupState();

      try {
        await verifySignInCode(email, code);
        authCodeElement.value = '';
        authFeedbackMessage = null;
        await refreshAuthState();

        if (popupMode === 'save' && lastScrapeResult) {
          setWorkflowStatus('Signed in. Review the extracted fields, then save them to ApplyVault.');
        } else {
          setWorkflowStatus(DEFAULT_READY_STATUS);
        }
      } catch (error) {
        authCodeElement.value = '';
        authFeedbackMessage = error instanceof Error ? error.message : 'Verifying the sign-in code failed.';
        authFeedbackTone = 'error';
      } finally {
        authPending = false;
        renderPopupState();
      }
    })();
  });

  signOutButtonElement.addEventListener('click', () => {
    void (async () => {
      authPending = true;
      authFeedbackMessage = 'Signing out from ApplyVault...';
      renderPopupState();

      try {
        await signOut();
        authCodeElement.value = '';
        authFeedbackMessage = null;
        await refreshAuthState();
        resetPopupState(UNAUTHENTICATED_STATUS);
        await clearPopupDraft();
      } catch (error) {
        authFeedbackMessage = error instanceof Error ? error.message : 'Sign out failed.';
        authFeedbackTone = 'error';
      } finally {
        authPending = false;
        renderPopupState();
      }
    })();
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

    if (!isAuthenticated()) {
      renderPopupState();
      setWorkflowStatus(UNAUTHENTICATED_STATUS);
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
      isAuthenticated()
    );

    try {
      const response = await sendSaveRequest(draft.result);

      if (!response.success) {
        renderPopupState();
        setWorkflowStatus(response.error);
        await savePopupDraft({ ...draft, statusMessage: response.error });
        return;
      }

      const successMessage = `Saved to the ASP.NET API at ${response.data.savedAt}. Record id: ${response.data.id}.`;
      resetPopupState(`${successMessage} Ready to extract the current page.`);
      await clearPopupDraft();
    } catch (error) {
      const errorMessage = error instanceof Error ? error.message : 'Saving the extracted result failed.';
      renderPopupState();
      setWorkflowStatus(errorMessage);
      await savePopupDraft({ ...draft, statusMessage: errorMessage });
    }
  });

  renewScrapeButton.addEventListener('click', async () => {
    resetPopupState('Draft cleared. Extract the current page when you are ready.');
    await clearPopupDraft();
  });

  window.addEventListener('beforeunload', () => {
    clearDraftSaveTimeout();
    const draft = buildCurrentDraft();
    if (draft) {
      void savePopupDraft(draft);
    }
  });
}
