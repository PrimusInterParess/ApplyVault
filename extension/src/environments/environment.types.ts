export interface ExtensionEnvironment {
  readonly production: boolean;
  readonly apiBaseUrl: string;
  readonly supabase: {
    readonly url: string;
    readonly anonKey: string;
  };
}
