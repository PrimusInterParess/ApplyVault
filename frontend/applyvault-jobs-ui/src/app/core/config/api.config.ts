import { InjectionToken } from '@angular/core';

import { environment } from '../../../environments/environment';

export interface ApiConfig {
  readonly baseUrl: string;
}

export const API_CONFIG = new InjectionToken<ApiConfig>('API_CONFIG');

export const defaultApiConfig: ApiConfig = {
  baseUrl: environment.apiBaseUrl
};
