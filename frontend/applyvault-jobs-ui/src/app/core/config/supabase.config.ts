import { InjectionToken } from '@angular/core';

export interface SupabaseConfig {
  readonly url: string;
  readonly anonKey: string;
}

export const SUPABASE_CONFIG = new InjectionToken<SupabaseConfig>('SUPABASE_CONFIG');

export const defaultSupabaseConfig: SupabaseConfig = {
  url: 'https://riglrazbjbucpyhaofvb.supabase.co',
  anonKey: 'sb_publishable_4X6_4OnkdMo2K2uBTWi0sg_mP4AJDBU'
};
