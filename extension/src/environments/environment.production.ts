import type { ExtensionEnvironment } from './environment.types';

/** Production — replace apiBaseUrl and manifest host_permissions before Chrome Web Store release. */
export const environment: ExtensionEnvironment = {
  production: true,
  apiBaseUrl: 'http://localhost:5173/api',
  supabase: {
    url: 'https://riglrazbjbucpyhaofvb.supabase.co',
    anonKey: 'sb_publishable_4X6_4OnkdMo2K2uBTWi0sg_mP4AJDBU'
  }
};
