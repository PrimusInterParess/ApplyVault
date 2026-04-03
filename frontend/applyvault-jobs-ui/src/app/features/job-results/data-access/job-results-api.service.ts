import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { API_CONFIG } from '../../../core/config/api.config';
import {
  CreateCalendarEventRequest,
  SavedJobResult,
  UpdateJobCaptureReviewRequest,
  UpdateJobDescriptionRequest,
  UpdateInterviewEventRequest
} from '../models/job-result.model';

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

  delete(id: string): Observable<void> {
    return this.httpClient.delete<void>(`${this.apiConfig.baseUrl}/scrape-results/${id}`);
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

  updateCaptureReview(
    id: string,
    request: UpdateJobCaptureReviewRequest
  ): Observable<SavedJobResult> {
    return this.httpClient.patch<SavedJobResult>(
      `${this.apiConfig.baseUrl}/scrape-results/${id}/capture-review`,
      request
    );
  }

  updateInterviewEvent(
    id: string,
    request: UpdateInterviewEventRequest
  ): Observable<SavedJobResult> {
    return this.httpClient.put<SavedJobResult>(
      `${this.apiConfig.baseUrl}/scrape-results/${id}/interview-event`,
      request
    );
  }

  clearInterviewEvent(id: string): Observable<SavedJobResult> {
    return this.httpClient.delete<SavedJobResult>(
      `${this.apiConfig.baseUrl}/scrape-results/${id}/interview-event`
    );
  }

  createCalendarEvent(
    id: string,
    request: CreateCalendarEventRequest
  ): Observable<unknown> {
    return this.httpClient.post(
      `${this.apiConfig.baseUrl}/scrape-results/${id}/calendar-events`,
      request
    );
  }
}
