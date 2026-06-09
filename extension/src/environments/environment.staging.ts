import type { ExtensionEnvironment } from './environment.types';

/** Staging — set apiBaseUrl to your staging API before `npm run build:staging`. */
export const environment: ExtensionEnvironment = {
  production: false,
  apiBaseUrl: 'http://localhost:5173/api',
  supabase: {
    url: 'https://riglrazbjbucpyhaofvb.supabase.co',
    anonKey: 'sb_publishable_4X6_4OnkdMo2K2uBTWi0sg_mP4AJDBU'
  }
};
