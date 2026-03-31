import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { API_CONFIG } from '../../../core/config/api.config';
import { SavedJobResult, UpdateJobDescriptionRequest } from '../models/job-result.model';

@Injectable({ providedIn: 'root' })
export class JobResultsApiService {
  private readonly httpClient = inject(HttpClient);
  private readonly apiConfig = inject(API_CONFIG);

  getAll(): Observable<readonly SavedJobResult[]> {
    return this.httpClient.get<readonly SavedJobResult[]>(
      `${this.apiConfig.baseUrl}/scrape-results`
    );
  }

  setRejected(id: string, isRejected: boolean): Observable<SavedJobResult> {
    return this.httpClient.patch<SavedJobResult>(
      `${this.apiConfig.baseUrl}/scrape-results/${id}/rejection`,
      { isRejected }
    );
  }

  updateDescription(
    id: string,
    request: UpdateJobDescriptionRequest
  ): Observable<SavedJobResult> {
    return this.httpClient.patch<SavedJobResult>(
      `${this.apiConfig.baseUrl}/scrape-results/${id}/description`,
      request
    );
  }
}
