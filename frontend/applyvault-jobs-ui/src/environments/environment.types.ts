export interface AppEnvironment {
  readonly production: boolean;
  readonly apiBaseUrl: string;
  readonly supabase: {
    readonly url: string;
    readonly anonKey: string;
  };
}
