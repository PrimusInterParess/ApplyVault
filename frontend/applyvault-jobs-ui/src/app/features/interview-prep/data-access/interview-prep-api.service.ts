import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { map, Observable } from 'rxjs';

import { API_CONFIG } from '../../../core/config/api.config';
import {
  InterviewPrepTurnRequest,
  InterviewPrepTurnResponse
} from '../models/interview-prep.model';

@Injectable({ providedIn: 'root' })
export class InterviewPrepApiService {
  private readonly httpClient = inject(HttpClient);
  private readonly apiConfig = inject(API_CONFIG);

  createTurn(request: InterviewPrepTurnRequest): Observable<InterviewPrepTurnResponse> {
    return this.httpClient
      .post<InterviewPrepTurnResponse>(`${this.apiConfig.baseUrl}/interview-prep/turns`, request)
      .pipe(map((response) => normalizeTurnResponse(response)));
  }
}

export function normalizeTurnResponse(
  response: InterviewPrepTurnResponse
): InterviewPrepTurnResponse {
  return {
    phase: response.phase || 'interview',
    inference: {
      role: response.inference?.role?.trim() || 'Unknown role',
      seniority: response.inference?.seniority?.trim() || 'unknown',
      interviewStyle: response.inference?.interviewStyle?.trim() || 'general',
      isTechnicalContext: Boolean(response.inference?.isTechnicalContext)
    },
    coachMessage: response.coachMessage?.trim() || '',
    scorecard: response.scorecard
      ? {
          overall: response.scorecard.overall,
          summary: response.scorecard.summary?.trim() || null,
          dimensions: Array.isArray(response.scorecard.dimensions)
            ? response.scorecard.dimensions
            : []
        }
      : null,
    followUps: Array.isArray(response.followUps)
      ? response.followUps.map((item) => item?.trim() ?? '').filter((item) => item.length > 0)
      : [],
    debriefBullets: Array.isArray(response.debriefBullets)
      ? response.debriefBullets.map((item) => item?.trim() ?? '').filter((item) => item.length > 0)
      : []
  };
}
