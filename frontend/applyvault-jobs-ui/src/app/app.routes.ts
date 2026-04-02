import { Routes } from '@angular/router';

import { authGuard, guestGuard } from './core/auth/auth.guard';

export const routes: Routes = [
  {
    path: '',
    pathMatch: 'full',
    redirectTo: 'login'
  },
  {
    path: 'login',
    canActivate: [guestGuard],
    title: 'ApplyVault Login',
    loadComponent: () =>
      import('./features/auth/pages/auth-page/auth-page.component').then(
        (module) => module.AuthPageComponent
      )
  },
  {
    path: 'jobs',
    title: 'ApplyVault Job Results',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/job-results/pages/job-results-page/job-results-page.component').then(
        (module) => module.JobResultsPageComponent
      )
  },
  {
    path: 'integrations/calendar/callback',
    title: 'Calendar Connection',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/job-results/pages/calendar-connect-callback/calendar-connect-callback.component').then(
        (module) => module.CalendarConnectCallbackComponent
      )
  },
  {
    path: '**',
    redirectTo: 'login'
  }
];
