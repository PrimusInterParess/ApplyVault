import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, Router, RouterStateSnapshot, UrlTree } from '@angular/router';

import { AuthService } from './auth.service';
import { authGuard, guestGuard } from './auth.guard';
import { createAuthServiceMock } from '../../../testing/auth-test-utils';

describe('authGuard', () => {
  let router: jasmine.SpyObj<Pick<Router, 'createUrlTree'>>;

  beforeEach(() => {
    router = jasmine.createSpyObj('Router', ['createUrlTree']);
    router.createUrlTree.and.returnValue({} as UrlTree);

    TestBed.configureTestingModule({
      providers: [{ provide: Router, useValue: router }]
    });
  });

  it('allows authenticated users', async () => {
    TestBed.overrideProvider(AuthService, {
      useValue: createAuthServiceMock({ authenticated: true })
    });

    const result = await runAuthGuard('/jobs');

    expect(result).toBe(true);
    expect(router.createUrlTree).not.toHaveBeenCalled();
  });

  it('redirects guests to login with redirectTo query param', async () => {
    TestBed.overrideProvider(AuthService, {
      useValue: createAuthServiceMock({ authenticated: false })
    });

    const result = await runAuthGuard('/jobs');

    expect(result).toEqual({} as UrlTree);
    expect(router.createUrlTree).toHaveBeenCalledWith(['/login'], {
      queryParams: { redirectTo: '/jobs' }
    });
  });
});

describe('guestGuard', () => {
  let router: jasmine.SpyObj<Pick<Router, 'createUrlTree'>>;

  beforeEach(() => {
    router = jasmine.createSpyObj('Router', ['createUrlTree']);
    router.createUrlTree.and.returnValue({} as UrlTree);

    TestBed.configureTestingModule({
      providers: [{ provide: Router, useValue: router }]
    });
  });

  it('allows guests to open login', async () => {
    TestBed.overrideProvider(AuthService, {
      useValue: createAuthServiceMock({ authenticated: false })
    });

    const result = await runGuestGuard();

    expect(result).toBe(true);
    expect(router.createUrlTree).not.toHaveBeenCalled();
  });

  it('redirects authenticated users away from login', async () => {
    TestBed.overrideProvider(AuthService, {
      useValue: createAuthServiceMock({ authenticated: true })
    });

    const result = await runGuestGuard();

    expect(result).toEqual({} as UrlTree);
    expect(router.createUrlTree).toHaveBeenCalledWith(['/jobs']);
  });
});

async function runAuthGuard(url: string) {
  return TestBed.runInInjectionContext(() =>
    authGuard({} as ActivatedRouteSnapshot, { url } as RouterStateSnapshot)
  );
}

async function runGuestGuard() {
  return TestBed.runInInjectionContext(() =>
    guestGuard({} as ActivatedRouteSnapshot, {} as RouterStateSnapshot)
  );
}
