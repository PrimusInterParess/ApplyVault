import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: '',
    pathMatch: 'full',
    redirectTo: 'jobs'
  },
  {
    path: 'jobs',
    title: 'ApplyVault Job Results',
    loadComponent: () =>
      import('./features/job-results/pages/job-results-page/job-results-page.component').then(
        (module) => module.JobResultsPageComponent
      )
  },
  {
    path: '**',
    redirectTo: 'jobs'
  }
];
