import { environment } from '../../environments/environment';

export const SUPABASE_URL = environment.supabase.url.trim();
export const SUPABASE_ANON_KEY = environment.supabase.anonKey.trim();

export function assertSupabaseConfigured(): void {
  if (!SUPABASE_URL) {
    throw new Error(
      'ApplyVault extension: Supabase URL is missing. Rebuild with `npm run build` after configuring src/environments/environment.ts.'
    );
  }

  if (!SUPABASE_ANON_KEY) {
    throw new Error(
      'ApplyVault extension: Supabase anon key is missing. Rebuild with `npm run build` after configuring src/environments/environment.ts.'
    );
  }
}
