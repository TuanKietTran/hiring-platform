import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import type { User } from '../models';

const ROOT = '/api/auth';

@Injectable({ providedIn: 'root' })
export class AuthApi {
  private readonly http = inject(HttpClient);

  me() {
    return this.http.get<User>(`${ROOT}/me`);
  }

  login(body: { email: string; password: string }) {
    return this.http.post<User>(`${ROOT}/login`, body);
  }

  registerCandidate(body: { email: string; password: string; fullName: string }) {
    return this.http.post<User>(`${ROOT}/register/candidate`, body);
  }

  registerCompany(body: {
    companyName: string;
    website: string | null;
    email: string;
    password: string;
    fullName: string;
  }) {
    return this.http.post<User>(`${ROOT}/register/company`, body);
  }

  logout() {
    return this.http.post<void>(`${ROOT}/logout`, {});
  }
}
