import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import type { Role, User } from '../models';

@Injectable({ providedIn: 'root' })
export class StaffApi {
  private readonly http = inject(HttpClient);

  list() {
    return this.http.get<User[]>('/api/staff');
  }

  add(body: { email: string; password: string; fullName: string; role: Role }) {
    return this.http.post<User>('/api/staff', body);
  }
}
