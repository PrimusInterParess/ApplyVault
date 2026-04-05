import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { API_CONFIG } from '../../../core/config/api.config';
import {
  ConnectedMailAccount,
  MailAuthorizationStartResponse
} from '../models/mail-connection.model';

@Injectable({ providedIn: 'root' })
export class MailConnectionsApiService {
  private readonly httpClient = inject(HttpClient);
  private readonly apiConfig = inject(API_CONFIG);

  getAll(): Observable<readonly ConnectedMailAccount[]> {
    return this.httpClient.get<readonly ConnectedMailAccount[]>(
      `${this.apiConfig.baseUrl}/mail-connections`
    );
  }

  startConnection(provider: string): Observable<MailAuthorizationStartResponse> {
    return this.httpClient.post<MailAuthorizationStartResponse>(
      `${this.apiConfig.baseUrl}/mail-connections/${provider}/start`,
      {
        returnUrl: `${window.location.origin}/integrations/mail/callback`
      }
    );
  }

  deleteConnection(id: string): Observable<void> {
    return this.httpClient.delete<void>(`${this.apiConfig.baseUrl}/mail-connections/${id}`);
  }
}
