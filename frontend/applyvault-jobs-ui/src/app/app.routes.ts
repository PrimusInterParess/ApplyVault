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
        path: 'search',
        title: 'Job Search',
        data: {
          shellSubtitle: 'Search public job listings from EURES and Work in Denmark.'
        },
        loadComponent: () =>
          import('./features/job-search/pages/job-search-page/job-search-page.component').then(
            (module) => module.JobSearchPageComponent
          )
      },
      {
        path: 'eures',
        redirectTo: 'search?source=eures',
        pathMatch: 'full'
      },
      {
        path: 'workindenmark',
        redirectTo: 'search?source=jobnet',
        pathMatch: 'full'
      },
      {
        path: 'my-cv',
        redirectTo: 'cv-builder',
        pathMatch: 'full'
      },
      {
        path: 'cv-builder',
        title: 'CV Builder',
        data: {
          shellSubtitle: 'Choose a template, edit your structured CV on the page, and export a PDF.'
        },
        loadComponent: () =>
          import('./features/cv-projects/pages/cv-builder-page/cv-builder-page.component').then(
            (module) => module.CvBuilderPageComponent
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
        path: 'interview-prep',
        title: 'Interview Prep',
        data: {
          shellSubtitle: 'Practice interviews grounded in your Structured CV.'
        },
        loadComponent: () =>
          import('./features/interview-prep/pages/interview-prep-page/interview-prep-page.component').then(
            (module) => module.InterviewPrepPageComponent
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
