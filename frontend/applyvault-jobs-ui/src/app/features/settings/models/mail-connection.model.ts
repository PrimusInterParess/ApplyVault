export interface ConnectedMailAccount {
  readonly id: string;
  readonly provider: string;
  readonly providerUserId: string;
  readonly email: string | null;
  readonly displayName: string | null;
  readonly expiresAt: string | null;
  readonly syncStatus: 'connected' | 'syncing' | 'error' | 'needs_reconnect' | string;
  readonly lastSyncedAt: string | null;
  readonly lastSyncError: string | null;
  readonly lastHistoryId: string | null;
  readonly createdAt: string;
  readonly updatedAt: string;
}

export interface MailAuthorizationStartResponse {
  readonly authorizationUrl: string;
}
