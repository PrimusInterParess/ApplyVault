import type { AppEnvironment } from './environment.types';

/** Production — set URLs/keys to match deploy `.env` before `ng build --configuration production`. */
export const environment: AppEnvironment = {
  production: true,
  apiBaseUrl: 'https://api.example.com/api',
  supabase: {
    url: 'https://your-project.supabase.co',
    anonKey: ''
  }
};
