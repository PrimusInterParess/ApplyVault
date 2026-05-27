import { InjectionToken } from '@angular/core';

import { environment } from '../../../environments/environment';

export interface SupabaseConfig {
  readonly url: string;
  readonly anonKey: string;
}

export const SUPABASE_CONFIG = new InjectionToken<SupabaseConfig>('SUPABASE_CONFIG');

export const defaultSupabaseConfig: SupabaseConfig = {
  url: environment.supabase.url,
  anonKey: environment.supabase.anonKey
};
