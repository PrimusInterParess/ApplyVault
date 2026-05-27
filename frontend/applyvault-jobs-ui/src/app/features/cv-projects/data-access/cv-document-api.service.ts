import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { API_CONFIG } from '../../../core/config/api.config';
import { CvDocument } from '../models/cv-document.model';
import { CvPdfSection } from '../models/cv-project.model';

@Injectable({ providedIn: 'root' })
export class CvDocumentApiService {
  private readonly httpClient = inject(HttpClient);
  private readonly apiConfig = inject(API_CONFIG);

  getCurrent(): Observable<CvDocument> {
    return this.httpClient.get<CvDocument>(`${this.apiConfig.baseUrl}/cv-documents/current`);
  }

  getCvSections(): Observable<readonly CvPdfSection[]> {
    return this.httpClient.get<readonly CvPdfSection[]>(`${this.apiConfig.baseUrl}/cv-documents/current/sections`);
  }

  upload(file: File): Observable<CvDocument> {
    const formData = new FormData();
    formData.append('file', file, file.name);

    return this.httpClient.post<CvDocument>(`${this.apiConfig.baseUrl}/cv-documents/current`, formData);
  }

  downloadContent(): Observable<Blob> {
    return this.httpClient.get(`${this.apiConfig.baseUrl}/cv-documents/current/content`, {
      responseType: 'blob'
    });
  }

  mergeProjects(): Observable<CvDocument> {
    return this.httpClient.post<CvDocument>(
      `${this.apiConfig.baseUrl}/cv-documents/current/merge-projects`,
      null
    );
  }

  delete(): Observable<void> {
    return this.httpClient.delete<void>(`${this.apiConfig.baseUrl}/cv-documents/current`);
  }
}
