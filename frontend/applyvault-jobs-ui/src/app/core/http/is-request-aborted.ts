import { HttpErrorResponse } from '@angular/common/http';

export function isRequestAborted(error: unknown): boolean {
  if (error instanceof HttpErrorResponse && error.status === 0) {
    return true;
  }

  if (error instanceof DOMException && error.name === 'AbortError') {
    return true;
  }

  return (
    typeof error === 'object' &&
    error !== null &&
    'name' in error &&
    (error as { name?: string }).name === 'AbortError'
  );
}
