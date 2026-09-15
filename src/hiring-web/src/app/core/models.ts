export type Role = 'candidate' | 'recruiter' | 'hiringManager' | 'orgAdmin';
export type WorkplaceType = 'onSite' | 'hybrid' | 'remote';
export type EmploymentType = 'fullTime' | 'partTime' | 'contract' | 'internship';
export type JobStatus = 'draft' | 'open' | 'paused' | 'closed';
export type ApplicationStage =
  'applied' | 'screening' | 'interviewing' | 'offered' | 'hired' | 'rejected' | 'withdrawn';
export type Currency = 'USD' | 'EUR' | 'GBP' | 'JPY' | 'AUD' | 'CAD' | 'SGD' | 'VND';

export interface User {
  id: string;
  email: string;
  fullName: string;
  role: Role;
  companyId: string | null;
  companyName: string | null;
}

export interface Salary {
  minMinor: number;
  maxMinor: number;
  currency: Currency;
}

export interface Job {
  id: string;
  companyId: string;
  companyName: string;
  title: string;
  description: string;
  location: string;
  employmentType: EmploymentType;
  workplaceType: WorkplaceType;
  salary: Salary | null;
  skills: string[];
  status: JobStatus;
  createdAt: string;
  publishedAt: string | null;
}

export interface JobInput {
  title: string;
  description: string;
  location: string;
  employmentType: EmploymentType;
  workplaceType: WorkplaceType;
  salary: Salary | null;
  skills: string[];
}

export interface PagedResult<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
}

export interface StageChange {
  from: ApplicationStage;
  to: ApplicationStage;
  changedBy: string;
  note: string | null;
  at: string;
}

export interface JobApplication {
  id: string;
  jobId: string;
  jobTitle: string;
  candidateId: string;
  candidateName: string;
  candidateEmail: string;
  coverLetter: string;
  resumeUrl: string | null;
  stage: ApplicationStage;
  nextStages: ApplicationStage[];
  history: StageChange[];
  appliedAt: string;
}

export interface BlobObject {
  id: string;
  name: string;
  contentType: string;
  size: number;
  purpose: string;
  url: string;
}

export interface CvDocument {
  id: string;
  name: string;
  url: string;
  isPrimary: boolean;
  addedAt: string;
}
export interface ApplicantProfile {
  candidateId: string;
  headline: string;
  summary: string;
  location: string;
  phone: string | null;
  phoneCallingCode: string | null;
  skills: string[];
  cvs: CvDocument[];
  updatedAt: string;
}
export interface ApplicantProfileInput {
  headline: string;
  summary: string;
  location: string;
  phone: string | null;
  phoneCallingCode: string | null;
  skills: string[];
}

export interface Interviewer {
  id: string;
  fullName: string;
}
export interface Interview {
  id: string;
  applicationId: string;
  kind: 'phone' | 'video' | 'onSite' | 'technical';
  startsAt: string;
  durationMinutes: number;
  interviewers: Interviewer[];
  status: 'scheduled' | 'completed' | 'cancelled';
}

export const isStaff = (user: User | null): boolean => !!user && user.role !== 'candidate';
export const enumLabel = (value: string): string =>
  value.replace(/([A-Z])/g, ' $1').replace(/^./, (c) => c.toUpperCase());
