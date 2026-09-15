import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import type { Interview } from '../models';

@Injectable({ providedIn: 'root' })
export class InterviewsApi {
  private readonly http = inject(HttpClient);

  listForApplication(applicationId: string) {
    return this.http.get<Interview[]>(`/api/applications/${applicationId}/interviews`);
  }
}
