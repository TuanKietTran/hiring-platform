import type { HttpInterceptorFn } from '@angular/common/http';
import { HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, tap, throwError } from 'rxjs';
import { ConnectivityService } from './connectivity.service';

export const connectivityInterceptor: HttpInterceptorFn = (request, next) => {
  const connectivity = inject(ConnectivityService);
  return next(request).pipe(
    tap(() => connectivity.markConnected()),
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse) connectivity.markRequestFailure(error.status);
      return throwError(() => error);
    }),
  );
};
