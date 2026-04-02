import { createClient, type Session } from '@supabase/supabase-js';

import { API_BASE_URL } from '../api/apiConfig';
import type { CurrentUser } from '../../shared/models/currentUser';
import { SUPABASE_ANON_KEY, SUPABASE_URL } from './supabaseConfig';

export interface ExtensionAuthState {
  readonly session: Session | null;
  readonly currentUser: CurrentUser | null;
  readonly apiError: string | null;
}

const SESSION_STORAGE_KEY = 'applyvault-extension-auth';

const chromeStorageAdapter = {
  async getItem(key: string): Promise<string | null> {
    const stored = await chrome.storage.local.get(key);
    const value = stored[key];
    return typeof value === 'string' ? value : null;
  },
  async setItem(key: string, value: string): Promise<void> {
    await chrome.storage.local.set({ [key]: value });
  },
  async removeItem(key: string): Promise<void> {
    await chrome.storage.local.remove(key);
  }
};

const supabase = createClient(SUPABASE_URL, SUPABASE_ANON_KEY, {
  auth: {
    autoRefreshToken: true,
    persistSession: true,
    detectSessionInUrl: false,
    storageKey: SESSION_STORAGE_KEY,
    storage: chromeStorageAdapter
  }
});

function getErrorMessage(error: unknown, fallback: string): string {
  if (error && typeof error === 'object' && 'message' in error && typeof error.message === 'string') {
    return error.message;
  }

  return fallback;
}

async function buildErrorMessage(response: Response): Promise<string> {
  const responseText = await response.text();
  const trimmedResponseText = responseText.trim();

  if (trimmedResponseText.length === 0) {
    return `ApplyVault API returned ${response.status} ${response.statusText}.`;
  }

  return `ApplyVault API returned ${response.status} ${response.statusText}: ${trimmedResponseText}`;
}

async function fetchCurrentUser(accessToken: string): Promise<CurrentUser> {
  const response = await fetch(`${API_BASE_URL}/auth/session`, {
    headers: {
      Authorization: `Bearer ${accessToken}`
    }
  });

  if (!response.ok) {
    throw new Error(await buildErrorMessage(response));
  }

  return (await response.json()) as CurrentUser;
}

export async function getSession(): Promise<Session | null> {
  const { data, error } = await supabase.auth.getSession();

  if (error) {
    throw new Error(getErrorMessage(error, 'Loading the Supabase session failed.'));
  }

  return data.session;
}

export async function requestSignInCode(email: string): Promise<void> {
  // Supabase sends a typed OTP when the Magic Link email template includes {{ .Token }}.
  const { error } = await supabase.auth.signInWithOtp({
    email,
    options: {
      shouldCreateUser: false
    }
  });

  if (error) {
    throw new Error(getErrorMessage(error, 'Sending the sign-in code failed.'));
  }
}

export async function verifySignInCode(email: string, code: string): Promise<Session> {
  const { data, error } = await supabase.auth.verifyOtp({
    email,
    token: code,
    type: 'email'
  });

  if (error) {
    throw new Error(getErrorMessage(error, 'Verifying the sign-in code failed.'));
  }

  if (!data.session) {
    throw new Error('Supabase did not return a session after verifying the sign-in code.');
  }

  return data.session;
}

export async function signOut(): Promise<void> {
  const { error } = await supabase.auth.signOut();

  if (error) {
    throw new Error(getErrorMessage(error, 'Sign out failed.'));
  }
}

export async function getAccessToken(): Promise<string | null> {
  return (await getSession())?.access_token ?? null;
}

export async function getAuthState(): Promise<ExtensionAuthState> {
  const session = await getSession();

  if (!session) {
    return {
      session: null,
      currentUser: null,
      apiError: null
    };
  }

  try {
    return {
      session,
      currentUser: await fetchCurrentUser(session.access_token),
      apiError: null
    };
  } catch (error) {
    return {
      session,
      currentUser: null,
      apiError: getErrorMessage(error, 'The ApplyVault API rejected the current session.')
    };
  }
}
