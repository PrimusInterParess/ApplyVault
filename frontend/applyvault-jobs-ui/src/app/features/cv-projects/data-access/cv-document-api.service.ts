import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { map, Observable } from 'rxjs';

import { API_CONFIG } from '../../../core/config/api.config';
import { CvDocument, CvDocumentUploadResult, CvStructuredReimportResult } from '../models/cv-document.model';
import {
  CvImprovementSuggestions,
  CvStructuredDocument,
  GenerateCvImprovementSuggestionsRequest,
  SaveCvStructuredDocumentRequest,
  UpdateCvStructuredWithAiRequest
} from '../models/cv-structured.model';

export interface CvFormattedPdfRequest {
  readonly templateId: number;
  readonly maxPages?: number | null;
}

export interface CvFormattedPdfResult {
  readonly blob: Blob;
  readonly pageCount: number | null;
  readonly maxPages: number | null;
  readonly exceedsLimit: boolean;
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

  downloadOriginalContent(): Observable<Blob> {
    return this.httpClient.get(
      `${this.apiConfig.baseUrl}/cv-documents/current/content/original/download`,
      { responseType: 'blob' }
    );
  }

  downloadFormattedPdf(request: CvFormattedPdfRequest): Observable<CvFormattedPdfResult> {
    let params = new HttpParams();

    if (request.templateId > 1) {
      params = params.set('templateId', String(request.templateId));
    }

    if (request.maxPages) {
      params = params.set('maxPages', String(request.maxPages));
    }

    return this.httpClient.get(`${this.apiConfig.baseUrl}/cv-documents/current/export/download`, {
      responseType: 'blob',
      observe: 'response',
      params
    }).pipe(
      map((response) => ({
        blob: response.body ?? new Blob([], { type: 'application/pdf' }),
        pageCount: this.readNumberHeader(response.headers.get('X-Cv-Export-Page-Count')),
        maxPages: this.readNumberHeader(response.headers.get('X-Cv-Export-Max-Pages')),
        exceedsLimit: response.headers.get('X-Cv-Export-Exceeds-Limit') === 'true',
        notice: this.readNoticeHeader(response.headers.get('X-Cv-Export-Notice'))
      }))
    );
  }

  delete(): Observable<void> {
    return this.httpClient.delete<void>(`${this.apiConfig.baseUrl}/cv-documents/current`);
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
