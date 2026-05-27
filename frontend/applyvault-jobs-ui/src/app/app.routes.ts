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
    path: '',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./shared/layout/app-shell.component').then((module) => module.AppShellComponent),
    children: [
      {
        path: 'jobs',
        title: 'ApplyVault Job Results',
        data: {
          shellSubtitle: 'Review saved jobs, refresh your workspace, and manage integrations.'
        },
        loadComponent: () =>
          import('./features/job-results/pages/job-results-page/job-results-page.component').then(
            (module) => module.JobResultsPageComponent
          )
      },
      {
        path: 'eures',
        title: 'EURES Job Search',
        data: {
          shellSubtitle: 'Search public job listings from the EURES portal.'
        },
        loadComponent: () =>
          import('./features/eures-jobs/pages/eures-jobs-page/eures-jobs-page.component').then(
            (module) => module.EuresJobsPageComponent
          )
      },
      {
        path: 'my-cv',
        title: 'My CV',
        data: {
          shellSubtitle: 'Upload your CV PDF, preview exports, and open the structured editor.'
        },
        loadComponent: () =>
          import('./features/cv-projects/pages/my-cv-page/my-cv-page.component').then(
            (module) => module.MyCvPageComponent
          )
      },
      {
        path: 'cv-editor',
        title: 'CV Editor',
        data: {
          shellSubtitle: 'Import, edit, and export a structured CV built from your PDF and project summaries.'
        },
        loadComponent: () =>
          import('./features/cv-projects/pages/cv-editor-page/cv-editor-page.component').then(
            (module) => module.CvEditorPageComponent
          )
      },
      {
        path: 'cv-projects',
        title: 'Projects',
        data: {
          shellSubtitle: 'Browse GitHub repositories and generate project summaries.'
        },
        loadComponent: () =>
          import('./features/cv-projects/pages/cv-projects-page/cv-projects-page.component').then(
            (module) => module.CvProjectsPageComponent
          )
      },
      {
        path: 'settings',
        title: 'ApplyVault Settings',
        data: {
          shellSubtitle: 'Manage calendar, GitHub, and mailbox integrations.'
        },
        loadComponent: () =>
          import('./features/settings/pages/user-settings-page/user-settings-page.component').then(
            (module) => module.UserSettingsPageComponent
          )
      }
    ]
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
    path: 'integrations/mail/callback',
    title: 'Mailbox Connection',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/settings/pages/mail-connect-callback/mail-connect-callback.component').then(
        (module) => module.MailConnectCallbackComponent
      )
  },
  {
    path: 'integrations/github/callback',
    title: 'GitHub Connection',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/settings/pages/github-connect-callback/github-connect-callback.component').then(
        (module) => module.GitHubConnectCallbackComponent
      )
  },
  {
    path: '**',
    title: 'Page Not Found',
    loadComponent: () =>
      import('./features/not-found/pages/not-found-page/not-found-page.component').then(
        (module) => module.NotFoundPageComponent
      )
  }
];
