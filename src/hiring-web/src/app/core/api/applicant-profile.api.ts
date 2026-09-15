import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import type { ApplicantProfile, ApplicantProfileInput } from '../models';

const ROOT = '/api/applicant/profile';

@Injectable({ providedIn: 'root' })
export class ApplicantProfileApi {
  private readonly http = inject(HttpClient);

  get() {
    return this.http.get<ApplicantProfile>(ROOT);
  }

  update(body: ApplicantProfileInput) {
    return this.http.put<ApplicantProfile>(ROOT, body);
  }

  addCv(body: { name: string; url: string }) {
    return this.http.post<ApplicantProfile>(`${ROOT}/cvs`, body);
  }

  setPrimaryCv(id: string) {
    return this.http.post<ApplicantProfile>(`${ROOT}/cvs/${id}/primary`, {});
  }

  removeCv(id: string) {
    return this.http.delete<ApplicantProfile>(`${ROOT}/cvs/${id}`);
  }
}
