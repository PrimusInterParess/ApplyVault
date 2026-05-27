import type { AppEnvironment } from './environment.types';

/** Default for `ng serve` and `ng build --configuration development`. */
export const environment: AppEnvironment = {
  production: false,
  apiBaseUrl: 'http://localhost:5173/api',
  supabase: {
    url: 'https://riglrazbjbucpyhaofvb.supabase.co',
    anonKey: 'sb_publishable_4X6_4OnkdMo2K2uBTWi0sg_mP4AJDBU'
  }
};
