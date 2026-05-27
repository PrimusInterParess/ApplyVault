import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { API_CONFIG } from '../../../core/config/api.config';
import {
  CvProjectSummary,
  GenerateCvProjectRequest,
  GitHubRepositoryListItem
} from '../models/cv-project.model';

@Injectable({ providedIn: 'root' })
export class CvProjectsApiService {
  private readonly httpClient = inject(HttpClient);
  private readonly apiConfig = inject(API_CONFIG);

  listRepositories(page: number, perPage = 100): Observable<readonly GitHubRepositoryListItem[]> {
    return this.httpClient.get<readonly GitHubRepositoryListItem[]>(
      `${this.apiConfig.baseUrl}/github/repos`,
      {
        params: {
          page: String(page),
          perPage: String(perPage)
        }
      }
    );
  }

  listSummaries(): Observable<readonly CvProjectSummary[]> {
    return this.httpClient.get<readonly CvProjectSummary[]>(`${this.apiConfig.baseUrl}/cv-projects`);
  }

  generateSummary(request: GenerateCvProjectRequest): Observable<CvProjectSummary> {
    return this.httpClient.post<CvProjectSummary>(`${this.apiConfig.baseUrl}/cv-projects/generate`, request);
  }

  deleteSummary(id: string): Observable<void> {
    return this.httpClient.delete<void>(`${this.apiConfig.baseUrl}/cv-projects/${id}`);
  }
}
