import { computed, signal } from '@angular/core';
import type { Session } from '@supabase/supabase-js';

import { AuthService, CurrentUser } from '../app/core/auth/auth.service';

export const TEST_ACCESS_TOKEN = 'test-access-token';

export const TEST_CURRENT_USER: CurrentUser = {
  id: 'user-1',
  supabaseUserId: 'supabase-user-1',
  email: 'tester@example.com',
  displayName: null
};

export function createMockSession(accessToken = TEST_ACCESS_TOKEN): Session {
  return {
    access_token: accessToken,
    refresh_token: 'test-refresh-token',
    expires_in: 3600,
    token_type: 'bearer',
    user: {
      id: TEST_CURRENT_USER.supabaseUserId,
      aud: 'authenticated',
      role: 'authenticated',
      email: TEST_CURRENT_USER.email,
      app_metadata: {},
      user_metadata: {}
    }
  } as Session;
}

export function createAuthServiceMock(options: {
  authenticated?: boolean;
  currentUser?: CurrentUser | null;
  accessToken?: string;
} = {}): Pick<
  AuthService,
  | 'initialized'
  | 'loading'
  | 'error'
  | 'session'
  | 'currentUser'
  | 'isAuthenticated'
  | 'accessToken'
  | 'ensureInitialized'
> {
  const authenticated = options.authenticated ?? true;
  const accessToken = options.accessToken ?? TEST_ACCESS_TOKEN;
  const sessionSignal = signal<Session | null>(
    authenticated ? createMockSession(accessToken) : null
  );
  const currentUserSignal = signal<CurrentUser | null>(
    authenticated ? (options.currentUser ?? TEST_CURRENT_USER) : null
  );

  return {
    initialized: signal(true),
    loading: signal(false),
    error: signal<string | null>(null),
    session: sessionSignal,
    currentUser: currentUserSignal,
    isAuthenticated: computed(() => sessionSignal() !== null),
    accessToken: computed(() => sessionSignal()?.access_token ?? null),
    ensureInitialized: jasmine.createSpy('ensureInitialized').and.returnValue(Promise.resolve())
  };
}
