import type { AppEnvironment } from './environment.types';

/** Pre-production — set URLs/keys to match staging API and Supabase before `ng build --configuration staging`. */
export const environment: AppEnvironment = {
  production: false,
  apiBaseUrl: 'https://api.staging.example.com/api',
  supabase: {
    url: 'https://your-project.supabase.co',
    anonKey: ''
  }
};
