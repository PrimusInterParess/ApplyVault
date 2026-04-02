import { HttpClient } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { createClient, type Session, type SupabaseClient } from '@supabase/supabase-js';
import { firstValueFrom } from 'rxjs';

import { API_CONFIG } from '../config/api.config';
import { SUPABASE_CONFIG } from '../config/supabase.config';

export interface CurrentUser {
  readonly id: string;
  readonly supabaseUserId: string;
  readonly email: string | null;
  readonly displayName: string | null;
}

const authLockOperations: Record<string, Promise<unknown>> = {};

async function withSupabaseAuthLock<T>(
  name: string,
  _acquireTimeout: number,
  fn: () => Promise<T>
): Promise<T> {
  const previousOperation = authLockOperations[name] ?? Promise.resolve();

  const currentOperation = (async () => {
    try {
      await previousOperation;
    } catch {
      // Ignore previous lock failures so later auth operations can continue.
    }

    return await fn();
  })();

  authLockOperations[name] = currentOperation.catch(() => null);

  return await currentOperation;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly apiConfig = inject(API_CONFIG);
  private readonly supabaseConfig = inject(SUPABASE_CONFIG);
  private readonly httpClient = inject(HttpClient);
  private readonly supabaseClient: SupabaseClient | null =
    this.supabaseConfig.url && this.supabaseConfig.anonKey
      ? createClient(this.supabaseConfig.url, this.supabaseConfig.anonKey, {
          auth: {
            autoRefreshToken: true,
            persistSession: true,
            detectSessionInUrl: true,
            // Avoid Navigator LockManager issues with Angular zone.js.
            lock: withSupabaseAuthLock
          }
        })
      : null;
  private initializationPromise: Promise<void> | null = null;
  private currentUserLoadPromise: Promise<void> | null = null;
  private loadedAccessToken: string | null = null;

  readonly initialized = signal(false);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly session = signal<Session | null>(null);
  readonly currentUser = signal<CurrentUser | null>(null);
  readonly isAuthenticated = computed(() => this.session() !== null);
  readonly accessToken = computed(() => this.session()?.access_token ?? null);

  constructor() {
    this.supabaseClient?.auth.onAuthStateChange((_event, session) => {
      this.session.set(session);

      if (!session) {
        this.currentUser.set(null);
        this.loadedAccessToken = null;
        return;
      }

      void this.loadCurrentUser();
    });
  }

  async ensureInitialized(): Promise<void> {
    if (this.initialized()) {
      return;
    }

    if (!this.initializationPromise) {
      this.initializationPromise = this.initialize();
    }

    await this.initializationPromise;
  }

  async signIn(email: string, password: string): Promise<void> {
    this.loading.set(true);
    this.error.set(null);

    try {
      if (!this.supabaseClient) {
        throw new Error('Supabase is not configured yet.');
      }

      const { data, error } = await this.supabaseClient.auth.signInWithPassword({
        email,
        password
      });

      if (error) {
        throw error;
      }

      this.session.set(data.session);
      await this.loadCurrentUser();
    } catch (error) {
      this.error.set(this.getErrorMessage(error, 'Sign in failed.'));
      throw error;
    } finally {
      this.loading.set(false);
    }
  }

  async signUp(email: string, password: string): Promise<string | null> {
    this.loading.set(true);
    this.error.set(null);

    try {
      if (!this.supabaseClient) {
        throw new Error('Supabase is not configured yet.');
      }

      const { data, error } = await this.supabaseClient.auth.signUp({
        email,
        password
      });

      if (error) {
        throw error;
      }

      this.session.set(data.session);

      if (data.session) {
        await this.loadCurrentUser();
        return null;
      }

      return 'Check your email to confirm the account before signing in.';
    } catch (error) {
      this.error.set(this.getErrorMessage(error, 'Sign up failed.'));
      throw error;
    } finally {
      this.loading.set(false);
    }
  }

  async signOut(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);

    try {
      if (!this.supabaseClient) {
        throw new Error('Supabase is not configured yet.');
      }

      const { error } = await this.supabaseClient.auth.signOut();

      if (error) {
        throw error;
      }

      this.session.set(null);
      this.currentUser.set(null);
      this.loadedAccessToken = null;
    } catch (error) {
      this.error.set(this.getErrorMessage(error, 'Sign out failed.'));
      throw error;
    } finally {
      this.loading.set(false);
    }
  }

  private async initialize(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);

    try {
      if (!this.supabaseConfig.url || !this.supabaseConfig.anonKey) {
        this.error.set('Supabase is not configured yet. Add the URL and anon key in the frontend config.');
        this.initialized.set(true);
        return;
      }

      const { data, error } = await this.supabaseClient!.auth.getSession();

      if (error) {
        throw error;
      }

      this.session.set(data.session);

      if (data.session) {
        await this.loadCurrentUser();
      }
    } catch (error) {
      this.error.set(this.getErrorMessage(error, 'Session initialization failed.'));
    } finally {
      this.loading.set(false);
      this.initialized.set(true);
    }
  }

  private async loadCurrentUser(): Promise<void> {
    const session = this.session();

    if (!session) {
      this.currentUser.set(null);
      this.loadedAccessToken = null;
      return;
    }

    if (this.loadedAccessToken === session.access_token && this.currentUser()) {
      return;
    }

    if (this.currentUserLoadPromise) {
      await this.currentUserLoadPromise;
      return;
    }

    this.currentUserLoadPromise = this.fetchCurrentUser(session.access_token);

    try {
      await this.currentUserLoadPromise;
    } finally {
      this.currentUserLoadPromise = null;
    }
  }

  private async fetchCurrentUser(accessToken: string): Promise<void> {
    try {
      const user = await firstValueFrom(
        this.httpClient.get<CurrentUser>(`${this.apiConfig.baseUrl}/auth/session`)
      );

      if (this.session()?.access_token !== accessToken) {
        return;
      }

      this.currentUser.set(user);
      this.loadedAccessToken = accessToken;
    } catch (error) {
      if (this.session()?.access_token !== accessToken) {
        return;
      }

      this.error.set(this.getErrorMessage(error, 'The API rejected the current session.'));
      this.currentUser.set(null);
      this.loadedAccessToken = null;
    }
  }

  private getErrorMessage(error: unknown, fallback: string): string {
    if (error && typeof error === 'object' && 'message' in error && typeof error.message === 'string') {
      return error.message;
    }

    return fallback;
  }
}
