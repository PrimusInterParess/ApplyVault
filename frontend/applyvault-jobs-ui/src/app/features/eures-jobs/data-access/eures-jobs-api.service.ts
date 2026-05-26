import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { API_CONFIG } from '../../../core/config/api.config';
import {
  EuresJobDetail,
  EuresJobSearchRequest,
  EuresJobSearchResponse,
  SaveEuresJobResponse
} from '../models/eures-job.model';

@Injectable({ providedIn: 'root' })
export class EuresJobsApiService {
  private readonly httpClient = inject(HttpClient);
  private readonly apiConfig = inject(API_CONFIG);

  search(request: EuresJobSearchRequest): Observable<EuresJobSearchResponse> {
    return this.httpClient.post<EuresJobSearchResponse>(
      `${this.apiConfig.baseUrl}/eures/jobs/search`,
      request
    );
  }

  getById(id: string, requestLanguage = 'en'): Observable<EuresJobDetail> {
    const params = new HttpParams().set('requestLanguage', requestLanguage);

    return this.httpClient.get<EuresJobDetail>(
      `${this.apiConfig.baseUrl}/eures/jobs/${encodeURIComponent(id)}`,
      { params }
    );
  }

  saveListing(id: string, requestLanguage = 'en'): Observable<SaveEuresJobResponse> {
    const params = new HttpParams().set('requestLanguage', requestLanguage);

    return this.httpClient.post<SaveEuresJobResponse>(
      `${this.apiConfig.baseUrl}/eures/jobs/${encodeURIComponent(id)}/save`,
      null,
      { params }
    );
  }
}
