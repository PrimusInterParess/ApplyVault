export interface ConnectedCalendarAccount {
  readonly id: string;
  readonly provider: string;
  readonly providerUserId: string;
  readonly email: string | null;
  readonly displayName: string | null;
  readonly expiresAt: string | null;
  readonly createdAt: string;
  readonly updatedAt: string;
}

export interface CalendarAuthorizationStartResponse {
  readonly authorizationUrl: string;
}
