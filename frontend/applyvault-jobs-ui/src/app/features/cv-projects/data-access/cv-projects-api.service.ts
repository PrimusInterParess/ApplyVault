import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { API_CONFIG } from '../../../core/config/api.config';
import {
  CvProjectSummary,
  CvProjectSummaryPlacement,
  GenerateCvProjectRequest,
  GitHubRepositoryListItem,
  GitHubRepositoryReadme,
  UpdateCvProjectSummaryPlacementsRequest
} from '../models/cv-project.model';

@Injectable({ providedIn: 'root' })
export class CvProjectsApiService {
  private readonly httpClient = inject(HttpClient);
  private readonly apiConfig = inject(API_CONFIG);

  listRepositories(page: number, perPage = 5): Observable<readonly GitHubRepositoryListItem[]> {
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

  getRepositoryReadme(fullName: string): Observable<GitHubRepositoryReadme> {
    return this.httpClient.get<GitHubRepositoryReadme>(`${this.apiConfig.baseUrl}/github/repos/readme`, {
      params: {
        fullName
      }
    });
  }

  listSummaries(page: number, perPage = 5): Observable<readonly CvProjectSummary[]> {
    return this.httpClient.get<readonly CvProjectSummary[]>(`${this.apiConfig.baseUrl}/cv-projects`, {
      params: {
        page: String(page),
        perPage: String(perPage)
      }
    });
  }

  listAllSummaries(): Observable<readonly CvProjectSummary[]> {
    return this.httpClient.get<readonly CvProjectSummary[]>(`${this.apiConfig.baseUrl}/cv-projects/all`);
  }

  updatePlacements(
    request: UpdateCvProjectSummaryPlacementsRequest
  ): Observable<readonly CvProjectSummary[]> {
    return this.httpClient.put<readonly CvProjectSummary[]>(
      `${this.apiConfig.baseUrl}/cv-projects/summaries/placements`,
      request
    );
  }

  generateSummary(request: GenerateCvProjectRequest): Observable<CvProjectSummary> {
    return this.httpClient.post<CvProjectSummary>(`${this.apiConfig.baseUrl}/cv-projects/generate`, request);
  }

  deleteSummary(id: string): Observable<void> {
    return this.httpClient.delete<void>(`${this.apiConfig.baseUrl}/cv-projects/${id}`);
  }
}
