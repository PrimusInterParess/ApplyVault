import { HttpErrorResponse } from '@angular/common/http';

export function readApiErrorMessage(error: HttpErrorResponse): string | null {
  const payload = error.error;

  if (typeof payload === 'string' && payload.trim().length > 0) {
    return payload.trim();
  }

  if (!payload || typeof payload !== 'object') {
    return null;
  }

  const record = payload as Record<string, unknown>;

  for (const key of ['message', 'detail', 'title'] as const) {
    const value = record[key];
    if (typeof value === 'string' && value.trim().length > 0) {
      return value.trim();
    }
  }

  const errors = record['errors'];
  if (errors && typeof errors === 'object') {
    const firstError = Object.values(errors as Record<string, unknown>).find(
      (value) => Array.isArray(value) && value.length > 0
    ) as string[] | undefined;

    if (firstError?.[0]) {
      return firstError[0];
    }
  }

  return null;
}

export function resolveHttpErrorMessage(
  error: unknown,
  options: {
    readonly fallback: string;
    readonly statusMessages?: Partial<Record<number, string>>;
  }
): string {
  if (!(error instanceof HttpErrorResponse)) {
    return options.fallback;
  }

  const apiMessage = readApiErrorMessage(error);
  if (apiMessage) {
    return apiMessage;
  }

  const statusMessage = options.statusMessages?.[error.status];
  if (statusMessage) {
    return statusMessage;
  }

  return options.fallback;
}
