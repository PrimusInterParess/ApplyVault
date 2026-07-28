import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, shareReplay } from 'rxjs';

import { environment } from '../../../../environments/environment';
import { CvSectionCatalogDocument } from '../models/cv-structured.model';

@Injectable({ providedIn: 'root' })
export class CvSectionCatalogService {
  private readonly http = inject(HttpClient);
  private readonly staticCatalog$ = this.http
    .get<CvSectionCatalogDocument>('/cv-section-catalog.json')
    .pipe(shareReplay(1));

  private readonly apiCatalog$ = this.http
    .get<CvSectionCatalogDocument>(`${environment.apiBaseUrl}/api/cv-documents/section-catalog`)
    .pipe(shareReplay(1));

  loadCatalog(preferApi = false): Observable<CvSectionCatalogDocument> {
    return preferApi ? this.apiCatalog$ : this.staticCatalog$;
  }
}
