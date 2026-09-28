import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import type { ActivatedRouteSnapshot, RouterStateSnapshot, UrlTree } from '@angular/router';
import { provideRouter, Router } from '@angular/router';
import { Subject } from 'rxjs';
import { AuthApi } from './api/auth.api';
import { authGuard, staffGuard } from './guards';
import type { User } from './models';

const candidate = {
  id: 'u1',
  email: 'c@hirelane.dev',
  fullName: 'Dev Candidate',
  role: 'candidate',
} as User;
const admin = { ...candidate, role: 'orgAdmin' } as User;

describe('route guards', () => {
  let me: Subject<User>;

  beforeEach(() => {
    me = new Subject<User>();
    TestBed.configureTestingModule({
      providers: [provideRouter([]), { provide: AuthApi, useValue: { me: () => me } }],
    });
  });

  const run = (guard: typeof authGuard) =>
    TestBed.runInInjectionContext(() =>
      guard({} as ActivatedRouteSnapshot, {} as RouterStateSnapshot),
    ) as Promise<boolean | UrlTree>;
  const path = (result: boolean | UrlTree) =>
    TestBed.inject(Router).serializeUrl(result as UrlTree);

  it('waits for session restoration before allowing a refreshed protected route', async () => {
    const result = run(authGuard);
    me.next(candidate);
    me.complete();
    expect(await result).toBe(true);
  });

  it('redirects to login when the restored session is unauthenticated', async () => {
    const result = run(authGuard);
    me.error(new HttpErrorResponse({ status: 401 }));
    expect(path(await result)).toBe('/login');
  });

  it('lets restored staff through the staff guard', async () => {
    const staff = run(staffGuard);
    me.next(admin);
    me.complete();
    expect(await staff).toBe(true);
  });

  it('sends a restored candidate from staff routes to the dashboard', async () => {
    const result = run(staffGuard);
    me.next(candidate);
    me.complete();
    expect(path(await result)).toBe('/dashboard');
  });
});
