import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { API_CONFIG } from '../../../core/config/api.config';
import { PreparationAnswerInput, PreparationContext, PreparationHelpResponse, PreparationPack, PreparationPackInput, PreparationProgress } from '../models/interview-prep.model';

@Injectable({ providedIn: 'root' })
export class InterviewPrepApiService {
  private readonly http = inject(HttpClient);
  private readonly base = `${inject(API_CONFIG).baseUrl}/interview-prep`;

  context(): Observable<PreparationContext> {
    return this.http.get<PreparationContext>(`${this.base}/context`);
  }

  list(): Observable<readonly PreparationPack[]> {
    return this.http.get<readonly PreparationPack[]>(`${this.base}/packs`);
  }

  generate(input: PreparationPackInput): Observable<PreparationPack> {
    return this.http.post<PreparationPack>(`${this.base}/packs`, input);
  }

  regenerate(pack: PreparationPack, input: PreparationPackInput): Observable<PreparationPack> {
    return this.http.post<PreparationPack>(`${this.base}/packs/${pack.id}/regenerate`, {
      expectedUpdatedAt: pack.updatedAt, input
    });
  }

  saveProgress(pack: PreparationPack, progress: PreparationProgress): Observable<PreparationPack> {
    return this.http.put<PreparationPack>(`${this.base}/packs/${pack.id}/progress`, {
      expectedUpdatedAt: pack.updatedAt, progress
    });
  }

  prepareDocument(pack: PreparationPack, documentId: string): Observable<PreparationPack> {
    return this.http.post<PreparationPack>(`${this.documentUrl(pack, documentId)}/discovery`, { expectedUpdatedAt: pack.updatedAt });
  }

  saveAnswers(pack: PreparationPack, documentId: string, answers: readonly PreparationAnswerInput[]): Observable<PreparationPack> {
    return this.http.put<PreparationPack>(`${this.documentUrl(pack, documentId)}/answers`, { expectedUpdatedAt: pack.updatedAt, answers });
  }

  writeDocument(pack: PreparationPack, documentId: string): Observable<PreparationPack> {
    return this.http.post<PreparationPack>(`${this.documentUrl(pack, documentId)}/generate`, { expectedUpdatedAt: pack.updatedAt });
  }

  help(pack: PreparationPack, documentId: string, questionId: string, request: string,
    draftAnswer: string, previousGuidance?: string): Observable<PreparationHelpResponse> {
    return this.http.post<PreparationHelpResponse>(`${this.documentUrl(pack, documentId)}/questions/${encodeURIComponent(questionId)}/help`, {
      expectedUpdatedAt: pack.updatedAt, request, draftAnswer, previousGuidance
    });
  }

  private documentUrl(pack: PreparationPack, documentId: string): string {
    return `${this.base}/packs/${pack.id}/documents/${encodeURIComponent(documentId)}`;
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/packs/${id}`);
  }
}
