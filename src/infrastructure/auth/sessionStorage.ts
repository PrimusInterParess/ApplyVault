export const SESSION_STORAGE_KEY = 'applyvault-extension-auth';

interface StoredAuthSession {
  readonly access_token?: string;
  readonly refresh_token?: string;
  readonly expires_at?: number;
}

const TOKEN_EXPIRY_MARGIN_MS = 60_000;

function parseStoredSession(raw: unknown): StoredAuthSession | null {
  if (raw == null) {
    return null;
  }

  if (typeof raw === 'string') {
    try {
      return JSON.parse(raw) as StoredAuthSession;
    } catch {
      return null;
    }
  }

  if (typeof raw === 'object') {
    return raw as StoredAuthSession;
  }

  return null;
}

export async function readStoredSession(): Promise<StoredAuthSession | null> {
  const stored = await chrome.storage.local.get(SESSION_STORAGE_KEY);
  return parseStoredSession(stored[SESSION_STORAGE_KEY]);
}

export async function getStoredAccessToken(): Promise<string | null> {
  const session = await readStoredSession();
  const accessToken = session?.access_token;

  if (typeof accessToken !== 'string' || accessToken.length === 0) {
    return null;
  }

  const expiresAt = session?.expires_at;
  if (typeof expiresAt === 'number' && expiresAt * 1000 - Date.now() < TOKEN_EXPIRY_MARGIN_MS) {
    return null;
  }

  return accessToken;
}

export async function clearStoredSession(): Promise<void> {
  await chrome.storage.local.remove([
    SESSION_STORAGE_KEY,
    `${SESSION_STORAGE_KEY}-user`,
    `${SESSION_STORAGE_KEY}-code-verifier`
  ]);
}
