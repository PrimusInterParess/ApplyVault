import { HttpClient, HttpHeaders, HttpResponse } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { map, Observable } from 'rxjs';

import { API_CONFIG } from '../../../core/config/api.config';
import {
  InterviewPrepAnswerRetryResult,
  InterviewPrepAnswerReview,
  InterviewPrepCandidateReport,
  InterviewPrepCompetencyResults,
  InterviewPrepCreateSessionRequest,
  InterviewPrepPanelDebrief,
  InterviewPrepSessionDetail,
  InterviewPrepSessionListResponse,
  InterviewPrepSessionSummary,
  InterviewPrepSubmitTurnRequest,
  InterviewPrepTranscript,
  InterviewPrepTurnSubmitResponse
} from '../models/interview-prep.model';

const BASE = 'interview-prep';

@Injectable({ providedIn: 'root' })
export class InterviewPrepApiService {
  private readonly httpClient = inject(HttpClient);
  private readonly apiConfig = inject(API_CONFIG);

  private url(path: string): string {
    return `${this.apiConfig.baseUrl}/${BASE}${path}`;
  }

  createSession(request: InterviewPrepCreateSessionRequest): Observable<InterviewPrepSessionSummary> {
    return this.httpClient.post<InterviewPrepSessionSummary>(this.url('/sessions'), request);
  }

  listSessions(): Observable<InterviewPrepSessionListResponse> {
    return this.httpClient.get<InterviewPrepSessionListResponse>(this.url('/sessions'));
  }

  getSession(id: string): Observable<{ detail: InterviewPrepSessionDetail; eTag: string }> {
    return this.httpClient
      .get<InterviewPrepSessionDetail>(this.url(`/sessions/${id}`), { observe: 'response' })
      .pipe(
        map((response) => ({
          detail: response.body!,
          eTag: this.readEtag(response)
        }))
      );
  }

  deleteSession(id: string): Observable<void> {
    return this.httpClient.delete<void>(this.url(`/sessions/${id}`));
  }

  prepareSession(id: string, ifMatch?: string | null): Observable<InterviewPrepSessionDetail> {
    return this.mutateSession(id, 'prepare', ifMatch);
  }

  startSession(id: string, ifMatch?: string | null): Observable<InterviewPrepSessionDetail> {
    return this.mutateSession(id, 'start', ifMatch);
  }

  pauseSession(id: string, ifMatch?: string | null): Observable<InterviewPrepSessionDetail> {
    return this.mutateSession(id, 'pause', ifMatch);
  }

  resumeSession(id: string, ifMatch?: string | null): Observable<InterviewPrepSessionDetail> {
    return this.mutateSession(id, 'resume', ifMatch);
  }

  cancelSession(id: string, ifMatch?: string | null): Observable<InterviewPrepSessionDetail> {
    return this.mutateSession(id, 'cancel', ifMatch);
  }

  completeSession(id: string, ifMatch?: string | null): Observable<InterviewPrepSessionDetail> {
    return this.mutateSession(id, 'complete', ifMatch);
  }

  submitTurn(
    sessionId: string,
    request: InterviewPrepSubmitTurnRequest,
    ifMatch: string
  ): Observable<{ result: InterviewPrepTurnSubmitResponse; eTag: string }> {
    const headers = new HttpHeaders({ 'If-Match': ifMatch });
    return this.httpClient
      .post<InterviewPrepTurnSubmitResponse>(
        this.url(`/sessions/${sessionId}/turns`),
        request,
        { headers, observe: 'response' }
      )
      .pipe(
        map((response) => ({
          result: response.body!,
          eTag: this.readEtag(response)
        }))
      );
  }

  getTranscript(sessionId: string): Observable<InterviewPrepTranscript> {
    return this.httpClient.get<InterviewPrepTranscript>(
      this.url(`/sessions/${sessionId}/transcript`)
    );
  }

  getReport(sessionId: string): Observable<InterviewPrepCandidateReport> {
    return this.httpClient.get<InterviewPrepCandidateReport>(
      this.url(`/sessions/${sessionId}/report`)
    );
  }

  getCompetencies(sessionId: string): Observable<InterviewPrepCompetencyResults> {
    return this.httpClient.get<InterviewPrepCompetencyResults>(
      this.url(`/sessions/${sessionId}/competencies`)
    );
  }

  requestAnswerReview(sessionId: string, turnId: string): Observable<InterviewPrepAnswerReview> {
    return this.httpClient.post<InterviewPrepAnswerReview>(
      this.url(`/sessions/${sessionId}/turns/${turnId}/review`),
      {}
    );
  }

  submitAnswerRetry(
    sessionId: string,
    turnId: string,
    revisedAnswer: string
  ): Observable<InterviewPrepAnswerRetryResult> {
    return this.httpClient.post<InterviewPrepAnswerRetryResult>(
      this.url(`/sessions/${sessionId}/turns/${turnId}/retry`),
      { revisedAnswer }
    );
  }

  getAnswerRetry(sessionId: string, turnId: string): Observable<InterviewPrepAnswerRetryResult> {
    return this.httpClient.get<InterviewPrepAnswerRetryResult>(
      this.url(`/sessions/${sessionId}/turns/${turnId}/retry`)
    );
  }

  startNextFullLoopStage(
    sessionId: string,
    ifMatch?: string | null
  ): Observable<InterviewPrepSessionDetail> {
    return this.mutateSession(sessionId, 'full-loop/next-stage', ifMatch);
  }

  getPanelDebrief(sessionId: string): Observable<InterviewPrepPanelDebrief> {
    return this.httpClient.get<InterviewPrepPanelDebrief>(
      this.url(`/sessions/${sessionId}/panel-debrief`)
    );
  }

  private mutateSession(
    id: string,
    action: string,
    ifMatch?: string | null
  ): Observable<InterviewPrepSessionDetail> {
    const headers = ifMatch ? new HttpHeaders({ 'If-Match': ifMatch }) : undefined;
    return this.httpClient
      .post<InterviewPrepSessionDetail>(this.url(`/sessions/${id}/${action}`), {}, { headers })
      .pipe(map((detail) => this.normalizeSessionEtag(detail)));
  }

  private normalizeSessionEtag(detail: InterviewPrepSessionDetail): InterviewPrepSessionDetail {
    if (detail.eTag) {
      return detail;
    }
    return detail;
  }

  private readEtag(response: HttpResponse<unknown>): string {
    const header = response.headers.get('ETag');
    if (header) {
      return header;
    }
    const body = response.body as InterviewPrepSessionDetail | null;
    return body?.eTag ?? '';
  }
}
