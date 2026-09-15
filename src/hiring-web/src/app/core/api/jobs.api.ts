import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import type { Job, JobInput, PagedResult, WorkplaceType } from '../models';

const ROOT = '/api';

@Injectable({ providedIn: 'root' })
export class JobsApi {
  private readonly http = inject(HttpClient);

  search(q = '', workplace: WorkplaceType | '' = '') {
    let params = new HttpParams().set('page', 1).set('pageSize', 50);
    if (q) params = params.set('q', q);
    if (workplace) params = params.set('workplace', workplace);
    return this.http.get<PagedResult<Job>>(`${ROOT}/jobs`, { params });
  }

  get(id: string) {
    return this.http.get<Job>(`${ROOT}/jobs/${id}`);
  }

  listForCompany() {
    return this.http.get<Job[]>(`${ROOT}/company/jobs`);
  }

  create(input: JobInput) {
    return this.http.post<Job>(`${ROOT}/jobs`, input);
  }

  changeStatus(id: string, change: 'publish' | 'pause' | 'close') {
    return this.http.post<Job>(`${ROOT}/jobs/${id}/status`, { change });
  }
}
