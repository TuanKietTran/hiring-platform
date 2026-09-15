import { HttpErrorResponse } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { AuthApi } from './api/auth.api';
import type { User } from './models';
import { isStaff } from './models';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly api = inject(AuthApi);
  readonly user = signal<User | null>(null);
  readonly ready = signal(false);
  readonly authenticated = computed(() => this.user() !== null);
  readonly staff = computed(() => isStaff(this.user()));

  async restore(): Promise<void> {
    try {
      this.user.set(await firstValueFrom(this.api.me()));
    } catch (error) {
      if (!(error instanceof HttpErrorResponse) || error.status !== 401) console.error(error);
    } finally {
      this.ready.set(true);
    }
  }

  setUser(user: User): void {
    this.user.set(user);
  }

  async logout(): Promise<void> {
    await firstValueFrom(this.api.logout());
    this.user.set(null);
  }
}
