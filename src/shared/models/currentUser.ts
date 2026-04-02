export interface CurrentUser {
  readonly id: string;
  readonly supabaseUserId: string;
  readonly email: string | null;
  readonly displayName: string | null;
}
