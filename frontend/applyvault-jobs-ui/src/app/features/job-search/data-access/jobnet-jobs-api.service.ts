import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { API_CONFIG } from '../../../core/config/api.config';
import {
  JobnetJobDetail,
  JobnetJobSearchRequest,
  JobnetJobSearchResponse,
  SaveJobnetJobResponse
} from '../models/jobnet-job.model';

@Injectable({ providedIn: 'root' })
export class JobnetJobsApiService {
  private readonly httpClient = inject(HttpClient);
  private readonly apiConfig = inject(API_CONFIG);

  search(request: JobnetJobSearchRequest): Observable<JobnetJobSearchResponse> {
    return this.httpClient.post<JobnetJobSearchResponse>(
      `${this.apiConfig.baseUrl}/jobnet/jobs/search`,
      request
    );
  }

  getById(id: string, requestLanguage = 'en'): Observable<JobnetJobDetail> {
    const params = new HttpParams().set('requestLanguage', requestLanguage);

    return this.httpClient.get<JobnetJobDetail>(
      `${this.apiConfig.baseUrl}/jobnet/jobs/${encodeURIComponent(id)}`,
      { params }
    );
  }

  saveListing(id: string, requestLanguage = 'en'): Observable<SaveJobnetJobResponse> {
    const params = new HttpParams().set('requestLanguage', requestLanguage);

    return this.httpClient.post<SaveJobnetJobResponse>(
      `${this.apiConfig.baseUrl}/jobnet/jobs/${encodeURIComponent(id)}/save`,
      null,
      { params }
    );
  }
}
