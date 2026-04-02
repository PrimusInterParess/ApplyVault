import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { API_CONFIG } from '../../../core/config/api.config';
import {
  CalendarAuthorizationStartResponse,
  ConnectedCalendarAccount
} from '../models/calendar-connection.model';

@Injectable({ providedIn: 'root' })
export class CalendarConnectionsApiService {
  private readonly httpClient = inject(HttpClient);
  private readonly apiConfig = inject(API_CONFIG);

  getAll(): Observable<readonly ConnectedCalendarAccount[]> {
    return this.httpClient.get<readonly ConnectedCalendarAccount[]>(
      `${this.apiConfig.baseUrl}/calendar-connections`
    );
  }

  startConnection(provider: string): Observable<CalendarAuthorizationStartResponse> {
    return this.httpClient.post<CalendarAuthorizationStartResponse>(
      `${this.apiConfig.baseUrl}/calendar-connections/${provider}/start`,
      {
        returnUrl: `${window.location.origin}/integrations/calendar/callback`
      }
    );
  }

  deleteConnection(id: string): Observable<void> {
    return this.httpClient.delete<void>(`${this.apiConfig.baseUrl}/calendar-connections/${id}`);
  }
}
