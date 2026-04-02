import { ApplicationConfig, provideZoneChangeDetection } from '@angular/core';
import { provideHttpClient, withFetch, withInterceptors } from '@angular/common/http';
import { provideRouter } from '@angular/router';

import { API_CONFIG, defaultApiConfig } from './core/config/api.config';
import { authInterceptor } from './core/auth/auth.interceptor';
import { SUPABASE_CONFIG, defaultSupabaseConfig } from './core/config/supabase.config';
import { routes } from './app.routes';

export const appConfig: ApplicationConfig = {
  providers: [
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideHttpClient(withFetch(), withInterceptors([authInterceptor])),
    provideRouter(routes),
    { provide: API_CONFIG, useValue: defaultApiConfig },
    { provide: SUPABASE_CONFIG, useValue: defaultSupabaseConfig }
  ]
};
