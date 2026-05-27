import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting
} from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { AuthService } from './auth.service';
import { authInterceptor } from './auth.interceptor';
import { createAuthServiceMock, TEST_ACCESS_TOKEN } from '../../../testing/auth-test-utils';

describe('authInterceptor', () => {
  let httpClient: HttpClient;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting()
      ]
    });

    httpClient = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('does not attach Authorization when there is no access token', () => {
    TestBed.overrideProvider(AuthService, {
      useValue: createAuthServiceMock({ authenticated: false })
    });

    httpClient.get('/api/protected').subscribe();

    const request = httpMock.expectOne('/api/protected');

    expect(request.request.headers.has('Authorization')).toBeFalse();
    request.flush({});
  });

  it('attaches Bearer token when session is present', () => {
    TestBed.overrideProvider(AuthService, {
      useValue: createAuthServiceMock({ authenticated: true, accessToken: TEST_ACCESS_TOKEN })
    });

    httpClient.get('/api/protected').subscribe();

    const request = httpMock.expectOne('/api/protected');

    expect(request.request.headers.get('Authorization')).toBe(`Bearer ${TEST_ACCESS_TOKEN}`);
    request.flush({});
  });
});
