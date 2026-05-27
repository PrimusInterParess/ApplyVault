export interface ConnectedGitHubAccount {
  readonly id: string;
  readonly provider: string;
  readonly providerUserId: string;
  readonly email: string | null;
  readonly displayName: string | null;
  readonly createdAt: string;
  readonly updatedAt: string;
}

export interface GitHubAuthorizationStartResponse {
  readonly authorizationUrl: string;
}
