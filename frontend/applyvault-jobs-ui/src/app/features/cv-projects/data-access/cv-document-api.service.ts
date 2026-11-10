import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { map, Observable } from 'rxjs';

import { API_CONFIG } from '../../../core/config/api.config';
import {
  CvDocument,
  CvDocumentUploadResult,
  CvStructuredReimportResult,
  ReplaceCvUnplacedContentRequest
} from '../models/cv-document.model';
import {
  CvImprovementSuggestions,
  CvQualityEvaluation,
  CvStructuredDocument,
  CvSummaryProposal,
  CvUpdateProposal,
  EvaluateCvQualityRequest,
  GenerateCvImprovementSuggestionsRequest,
  ProposeCvSummaryRequest,
  SaveCvStructuredDocumentRequest,
  UpdateCvStructuredWithAiRequest
} from '../models/cv-structured.model';

export interface CvFormattedPdfResult {
  readonly blob: Blob;
  readonly pageCount: number | null;
  readonly exceedsLimit: boolean;
  readonly notice: string | null;
}

/** GET current/export/preview — HTML body plus additive compact/notice headers. */
export interface CvExportPreviewHtmlResult {
  readonly html: string;
  readonly compactLevel: number | null;
  readonly notice: string | null;
}

@Injectable({ providedIn: 'root' })
export class CvDocumentApiService {
  private readonly httpClient = inject(HttpClient);
  private readonly apiConfig = inject(API_CONFIG);

  getCurrent(): Observable<CvDocument> {
    return this.httpClient.get<CvDocument>(`${this.apiConfig.baseUrl}/cv-documents/current`);
  }

  upload(file: File): Observable<CvDocumentUploadResult> {
    const formData = new FormData();
    formData.append('file', file, file.name);

    return this.httpClient.post<CvDocumentUploadResult>(
      `${this.apiConfig.baseUrl}/cv-documents/current`,
      formData
    );
  }

  downloadProfilePhoto(): Observable<Blob> {
    return this.httpClient.get(`${this.apiConfig.baseUrl}/cv-documents/current/profile-photo`, {
      responseType: 'blob'
    });
  }

  uploadProfilePhoto(file: File): Observable<CvDocument> {
    const formData = new FormData();
    formData.append('file', file, file.name);

    return this.httpClient.put<CvDocument>(
      `${this.apiConfig.baseUrl}/cv-documents/current/profile-photo`,
      formData
    );
  }

  deleteProfilePhoto(): Observable<CvDocument> {
    return this.httpClient.delete<CvDocument>(
      `${this.apiConfig.baseUrl}/cv-documents/current/profile-photo`
    );
  }

  downloadOriginalContent(): Observable<Blob> {
    return this.httpClient.get(
      `${this.apiConfig.baseUrl}/cv-documents/current/content/original/download`,
      { responseType: 'blob' }
    );
  }

  downloadFormattedPdf(): Observable<CvFormattedPdfResult> {
    return this.httpClient.get(`${this.apiConfig.baseUrl}/cv-documents/current/export/download`, {
      responseType: 'blob',
      observe: 'response'
    }).pipe(
      map((response) => ({
        blob: response.body ?? new Blob([], { type: 'application/pdf' }),
        pageCount: this.readNumberHeader(response.headers.get('X-Cv-Export-Page-Count')),
        exceedsLimit: response.headers.get('X-Cv-Export-Exceeds-Limit') === 'true',
        notice: this.readNoticeHeader(response.headers.get('X-Cv-Export-Notice'))
      }))
    );
  }

  /**
   * M1: authenticated HTML identical to Puppeteer input (text/html).
   * GET /api/cv-documents/current/export/preview
   * Reads additive X-Cv-Export-Compact-Level / X-Cv-Export-Notice when exposed.
   */
  getExportPreviewHtml(): Observable<CvExportPreviewHtmlResult> {
    return this.httpClient
      .get(`${this.apiConfig.baseUrl}/cv-documents/current/export/preview`, {
        responseType: 'text',
        observe: 'response'
      })
      .pipe(
        map((response) => ({
          html: response.body ?? '',
          compactLevel: this.readNumberHeader(response.headers.get('X-Cv-Export-Compact-Level')),
          notice: this.readNoticeHeader(response.headers.get('X-Cv-Export-Notice'))
        }))
      );
  }

  delete(): Observable<void> {
    return this.httpClient.delete<void>(`${this.apiConfig.baseUrl}/cv-documents/current`);
  }

  startBlank(): Observable<CvDocument> {
    return this.httpClient.post<CvDocument>(`${this.apiConfig.baseUrl}/cv-documents/current/start-blank`, {});
  }

  replaceUnplacedContent(request: ReplaceCvUnplacedContentRequest): Observable<CvDocument> {
    return this.httpClient.put<CvDocument>(
      `${this.apiConfig.baseUrl}/cv-documents/current/unplaced-content`,
      request
    );
  }

  getStructured(): Observable<CvStructuredDocument> {
    return this.httpClient.get<CvStructuredDocument>(
      `${this.apiConfig.baseUrl}/cv-documents/current/structured`
    );
  }

  saveStructured(request: SaveCvStructuredDocumentRequest): Observable<CvStructuredDocument> {
    return this.httpClient.put<CvStructuredDocument>(
      `${this.apiConfig.baseUrl}/cv-documents/current/structured`,
      request
    );
  }

  reimportStructured(): Observable<CvStructuredReimportResult> {
    return this.httpClient.post<CvStructuredReimportResult>(
      `${this.apiConfig.baseUrl}/cv-documents/current/structured/reimport`,
      {}
    );
  }

  updateStructuredWithAi(
    instructions: string,
    sectionIds?: readonly string[]
  ): Observable<CvStructuredDocument> {
    const request: UpdateCvStructuredWithAiRequest = { instructions };

    if (sectionIds && sectionIds.length > 0) {
      request.sectionIds = [...sectionIds];
    }

    return this.httpClient.post<CvStructuredDocument>(
      `${this.apiConfig.baseUrl}/cv-documents/current/structured/ai-update`,
      request
    );
  }

  /**
   * Ephemeral multi-section Update proposal — does not persist.
   * Approve uses local merge + existing PUT save (not ai-update).
   */
  proposeStructuredUpdate(
    instructions: string,
    sectionIds?: readonly string[]
  ): Observable<CvUpdateProposal> {
    const request: UpdateCvStructuredWithAiRequest = { instructions };

    if (sectionIds && sectionIds.length > 0) {
      request.sectionIds = [...sectionIds];
    }

    return this.httpClient.post<CvUpdateProposal>(
      `${this.apiConfig.baseUrl}/cv-documents/current/structured/ai-update-propose`,
      request
    );
  }

  generateStructuredSuggestions(
    sectionIds?: readonly string[],
    maxSuggestions = 6
  ): Observable<CvImprovementSuggestions> {
    const request: GenerateCvImprovementSuggestionsRequest = { maxSuggestions };

    if (sectionIds && sectionIds.length > 0) {
      request.sectionIds = [...sectionIds];
    }

    return this.httpClient.post<CvImprovementSuggestions>(
      `${this.apiConfig.baseUrl}/cv-documents/current/structured/ai-suggestions`,
      request
    );
  }

  /** Ephemeral quality evaluation — response must stay in-memory only (D2). */
  evaluateStructuredQuality(maxFindings = 8): Observable<CvQualityEvaluation> {
    const request: EvaluateCvQualityRequest = { maxFindings };

    return this.httpClient.post<CvQualityEvaluation>(
      `${this.apiConfig.baseUrl}/cv-documents/current/structured/ai-evaluation`,
      request
    );
  }

  /**
   * Ephemeral Summary regeneration proposal — does not persist.
   * Approve uses local Summary patch + existing PUT save (not ai-update).
   */
  proposeSummaryRegeneration(instructions?: string): Observable<CvSummaryProposal> {
    const trimmed = instructions?.trim();
    const request: ProposeCvSummaryRequest = {};

    if (trimmed) {
      request.instructions = trimmed;
    }

    return this.httpClient.post<CvSummaryProposal>(
      `${this.apiConfig.baseUrl}/cv-documents/current/structured/ai-summary-propose`,
      request
    );
  }

  private readNumberHeader(value: string | null): number | null {
    if (!value) {
      return null;
    }

    const parsed = Number.parseInt(value, 10);
    return Number.isInteger(parsed) ? parsed : null;
  }

  private readNoticeHeader(value: string | null): string | null {
    if (!value) {
      return null;
    }

    try {
      return decodeURIComponent(value);
    } catch {
      return value;
    }
  }
}
