import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import type { ApplicationStage, Interview, Job, JobApplication, JobInput, PagedResult, Role, User, WorkplaceType } from './models';

@Injectable({ providedIn: 'root' })
export class ApiService {
  private readonly http = inject(HttpClient);
  private readonly root = '/api';

  me() { return this.http.get<User>(`${this.root}/auth/me`); }
  login(body: { email: string; password: string }) { return this.http.post<User>(`${this.root}/auth/login`, body); }
  registerCandidate(body: { email: string; password: string; fullName: string }) { return this.http.post<User>(`${this.root}/auth/register/candidate`, body); }
  registerCompany(body: { companyName: string; website: string | null; email: string; password: string; fullName: string }) { return this.http.post<User>(`${this.root}/auth/register/company`, body); }
  logout() { return this.http.post<void>(`${this.root}/auth/logout`, {}); }

  searchJobs(q = '', workplace: WorkplaceType | '' = '') {
    let params = new HttpParams().set('page', 1).set('pageSize', 50);
    if (q) params = params.set('q', q);
    if (workplace) params = params.set('workplace', workplace);
    return this.http.get<PagedResult<Job>>(`${this.root}/jobs`, { params });
  }
  getJob(id: string) { return this.http.get<Job>(`${this.root}/jobs/${id}`); }
  companyJobs() { return this.http.get<Job[]>(`${this.root}/company/jobs`); }
  createJob(input: JobInput) { return this.http.post<Job>(`${this.root}/jobs`, input); }
  changeJobStatus(id: string, change: 'publish' | 'pause' | 'close') { return this.http.post<Job>(`${this.root}/jobs/${id}/status`, { change }); }

  apply(jobId: string, coverLetter: string, resumeUrl: string | null) { return this.http.post<JobApplication>(`${this.root}/jobs/${jobId}/applications`, { coverLetter, resumeUrl }); }
  myApplications() { return this.http.get<JobApplication[]>(`${this.root}/applications/mine`); }
  jobApplications(jobId: string) { return this.http.get<JobApplication[]>(`${this.root}/jobs/${jobId}/applications`); }
  advance(id: string, to: ApplicationStage, note: string | null) { return this.http.post<JobApplication>(`${this.root}/applications/${id}/advance`, { to, note }); }
  withdraw(id: string) { return this.http.post<JobApplication>(`${this.root}/applications/${id}/withdraw`, {}); }
  interviews(applicationId: string) { return this.http.get<Interview[]>(`${this.root}/applications/${applicationId}/interviews`); }

  staff() { return this.http.get<User[]>(`${this.root}/staff`); }
  addStaff(body: { email: string; password: string; fullName: string; role: Role }) { return this.http.post<User>(`${this.root}/staff`, body); }
}
