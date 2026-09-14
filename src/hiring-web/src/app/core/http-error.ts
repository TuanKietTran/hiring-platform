import { HttpErrorResponse } from '@angular/common/http';

const isRecord = (value: unknown): value is Record<string, unknown> =>
  typeof value === 'object' && value !== null;

export function httpErrorMessage(error: unknown, fallback: string): string {
  if (!(error instanceof HttpErrorResponse) || !isRecord(error.error)) return fallback;

  const direct = error.error['error'];
  if (typeof direct === 'string') return direct;

  const errors = error.error['errors'];
  if (!isRecord(errors)) return fallback;

  const request = errors['request'];
  return Array.isArray(request) && typeof request[0] === 'string' ? request[0] : fallback;
}
