import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import type { ApplicationStage, JobApplication } from '../models';

const ROOT = '/api';

@Injectable({ providedIn: 'root' })
export class ApplicationsApi {
  private readonly http = inject(HttpClient);

  apply(jobId: string, coverLetter: string, resumeUrl: string | null) {
    return this.http.post<JobApplication>(`${ROOT}/jobs/${jobId}/applications`, {
      coverLetter,
      resumeUrl,
    });
  }

  listMine() {
    return this.http.get<JobApplication[]>(`${ROOT}/applications/mine`);
  }

  listForJob(jobId: string) {
    return this.http.get<JobApplication[]>(`${ROOT}/jobs/${jobId}/applications`);
  }

  advance(id: string, to: ApplicationStage, note: string | null) {
    return this.http.post<JobApplication>(`${ROOT}/applications/${id}/advance`, { to, note });
  }

  withdraw(id: string) {
    return this.http.post<JobApplication>(`${ROOT}/applications/${id}/withdraw`, {});
  }
}
