import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { API_CONFIG } from '../../../core/config/api.config';
import { CvDocument, CvDocumentUploadResult } from '../models/cv-document.model';
import { CvStructuredDocument, SaveCvStructuredDocumentRequest } from '../models/cv-structured.model';

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

  downloadFormattedPdf(): Observable<Blob> {
    return this.httpClient.get(`${this.apiConfig.baseUrl}/cv-documents/current/export/download`, {
      responseType: 'blob'
    });
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
}
