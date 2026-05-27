import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { API_CONFIG } from '../../../core/config/api.config';
import {
  ConnectedGitHubAccount,
  GitHubAuthorizationStartResponse
} from '../models/github-connection.model';

@Injectable({ providedIn: 'root' })
export class GitHubConnectionsApiService {
  private readonly httpClient = inject(HttpClient);
  private readonly apiConfig = inject(API_CONFIG);

  getAll(): Observable<readonly ConnectedGitHubAccount[]> {
    return this.httpClient.get<readonly ConnectedGitHubAccount[]>(
      `${this.apiConfig.baseUrl}/github-connections`
    );
  }

  startConnection(provider: string): Observable<GitHubAuthorizationStartResponse> {
    return this.httpClient.post<GitHubAuthorizationStartResponse>(
      `${this.apiConfig.baseUrl}/github-connections/${provider}/start`,
      {
        returnUrl: `${window.location.origin}/integrations/github/callback`
      }
    );
  }

  deleteConnection(id: string): Observable<void> {
    return this.httpClient.delete<void>(`${this.apiConfig.baseUrl}/github-connections/${id}`);
  }
}
