import { inject } from '@angular/core';
import type { CanActivateFn} from '@angular/router';
import { Router } from '@angular/router';
import { AuthService } from './auth.service';

export const authGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  return auth.authenticated() || inject(Router).createUrlTree(['/login']);
};

export const staffGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  return auth.staff() || inject(Router).createUrlTree(['/dashboard']);
};
