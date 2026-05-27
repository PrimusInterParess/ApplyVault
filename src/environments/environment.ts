import type { ExtensionEnvironment } from './environment.types';

/** Default for `npm run build` and `npm run watch` (local API). */
export const environment: ExtensionEnvironment = {
  production: false,
  apiBaseUrl: 'http://localhost:5173/api',
  supabase: {
    url: 'https://riglrazbjbucpyhaofvb.supabase.co',
    anonKey: 'sb_publishable_4X6_4OnkdMo2K2uBTWi0sg_mP4AJDBU'
  }
};
