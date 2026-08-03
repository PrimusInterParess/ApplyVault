import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { map, Observable } from 'rxjs';

import { API_CONFIG } from '../../../core/config/api.config';
import {
  InterviewPrepCreateSessionRequest,
  InterviewPrepSessionDetail,
  InterviewPrepSessionListResponse,
  InterviewPrepSessionMessage,
  InterviewPrepSessionSummary,
  InterviewPrepTurnRequest,
  InterviewPrepTurnResponse
} from '../models/interview-prep.model';

@Injectable({ providedIn: 'root' })
export class InterviewPrepApiService {
  private readonly httpClient = inject(HttpClient);
  private readonly apiConfig = inject(API_CONFIG);

  private sessionsUrl(): string {
    return `${this.apiConfig.baseUrl}/interview-prep/sessions`;
  }

  createSession(request: InterviewPrepCreateSessionRequest): Observable<InterviewPrepSessionSummary> {
    return this.httpClient
      .post<InterviewPrepSessionSummary>(this.sessionsUrl(), request)
      .pipe(map((response) => normalizeSessionSummary(response)));
  }

  listSessions(options: { take?: number; skip?: number } = {}): Observable<InterviewPrepSessionListResponse> {
    let params = new HttpParams();
    if (options.take != null) {
      params = params.set('take', String(options.take));
    }
    if (options.skip != null) {
      params = params.set('skip', String(options.skip));
    }

    return this.httpClient
      .get<InterviewPrepSessionListResponse>(this.sessionsUrl(), { params })
      .pipe(
        map((response) => ({
          items: Array.isArray(response.items)
            ? response.items.map((item) => normalizeSessionSummary(item))
            : [],
          totalCount: typeof response.totalCount === 'number' ? response.totalCount : 0
        }))
      );
  }

  getSession(sessionId: string): Observable<InterviewPrepSessionDetail> {
    return this.httpClient
      .get<InterviewPrepSessionDetail>(`${this.sessionsUrl()}/${encodeURIComponent(sessionId)}`)
      .pipe(map((response) => normalizeSessionDetail(response)));
  }

  deleteSession(sessionId: string): Observable<void> {
    return this.httpClient.delete<void>(
      `${this.sessionsUrl()}/${encodeURIComponent(sessionId)}`
    );
  }

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
      : [],
    modelAnswer: normalizeOptionalText(response.modelAnswer),
    sessionId: normalizeOptionalId(response.sessionId)
  };
}

function normalizeSessionSummary(response: InterviewPrepSessionSummary): InterviewPrepSessionSummary {
  return {
    id: String(response.id ?? ''),
    mode: response.mode || 'behavioral',
    languageMix: response.languageMix || 'en',
    hiringMarket: response.hiringMarket || 'general',
    scrapeResultId: normalizeOptionalId(response.scrapeResultId),
    jobTitle: normalizeOptionalText(response.jobTitle),
    companyName: normalizeOptionalText(response.companyName),
    status: response.status || 'in_progress',
    phase: response.phase || 'interview',
    latestOverallScore:
      typeof response.latestOverallScore === 'number' ? response.latestOverallScore : null,
    createdAt: response.createdAt || '',
    updatedAt: response.updatedAt || '',
    completedAt: response.completedAt ?? null
  };
}

function normalizeSessionDetail(response: InterviewPrepSessionDetail): InterviewPrepSessionDetail {
  const summary = normalizeSessionSummary(response);
  const messages = Array.isArray(response.messages)
    ? [...response.messages]
        .map((message) => normalizeSessionMessage(message))
        .sort((left, right) => left.sequence - right.sequence)
    : [];

  return {
    ...summary,
    messages
  };
}

function normalizeSessionMessage(message: InterviewPrepSessionMessage): InterviewPrepSessionMessage {
  return {
    id: String(message.id ?? ''),
    sequence: typeof message.sequence === 'number' ? message.sequence : 0,
    role: message.role === 'user' ? 'user' : 'coach',
    text: message.text?.trim() || '',
    phase: message.phase || 'interview',
    scorecard: message.scorecard
      ? {
          overall: message.scorecard.overall,
          summary: message.scorecard.summary?.trim() || null,
          dimensions: Array.isArray(message.scorecard.dimensions)
            ? message.scorecard.dimensions
            : []
        }
      : null,
    followUps: Array.isArray(message.followUps)
      ? message.followUps.map((item) => item?.trim() ?? '').filter((item) => item.length > 0)
      : [],
    debriefBullets: Array.isArray(message.debriefBullets)
      ? message.debriefBullets.map((item) => item?.trim() ?? '').filter((item) => item.length > 0)
      : [],
    modelAnswer: normalizeOptionalText(message.modelAnswer),
    inference: message.inference
      ? {
          role: message.inference.role?.trim() || 'Unknown role',
          seniority: message.inference.seniority?.trim() || 'unknown',
          interviewStyle: message.inference.interviewStyle?.trim() || 'general',
          isTechnicalContext: Boolean(message.inference.isTechnicalContext)
        }
      : null,
    createdAt: message.createdAt || ''
  };
}

function normalizeOptionalText(value: string | null | undefined): string | null {
  const trimmed = value?.trim() ?? '';
  return trimmed.length > 0 ? trimmed : null;
}

function normalizeOptionalId(value: string | null | undefined): string | null {
  const trimmed = value?.trim() ?? '';
  return trimmed.length > 0 ? trimmed : null;
}
