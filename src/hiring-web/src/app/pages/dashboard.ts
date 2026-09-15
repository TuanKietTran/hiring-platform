import type { OnInit } from '@angular/core';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ApplicationsApi } from '../core/api/applications.api';
import { JobsApi } from '../core/api/jobs.api';
import { AuthService } from '../core/auth.service';
import { httpErrorMessage } from '../core/http-error';
import type { ApplicationStage, Job, JobApplication, JobInput } from '../core/models';
import { enumLabel } from '../core/models';

@Component({
  imports: [FormsModule, RouterLink],
  template: `
    <main class="page dashboard">
      <div class="page-heading">
        <p class="eyebrow">YOUR WORKSPACE</p>
        <h1>Good to see you, {{ auth.user()?.fullName?.split(' ')?.[0] }}.</h1>
        <p>
          {{
            auth.staff()
              ? 'Keep every role and candidate moving with clarity.'
              : 'Track your applications and discover what comes next.'
          }}
        </p>
      </div>
      @if (error()) {
        <div class="alert">{{ error() }}</div>
      }
      @if (auth.staff()) {
        <div class="dash-actions">
          <button class="button primary" (click)="showForm.set(!showForm())">
            {{ showForm() ? 'Cancel' : '+ New role' }}
          </button>
        </div>
        @if (showForm()) {
          <form class="panel job-form" (ngSubmit)="createJob()">
            <h2>Create a role</h2>
            <div class="form-grid">
              <label>Title<input required name="title" [(ngModel)]="draft.title" /></label
              ><label>Location<input name="location" [(ngModel)]="draft.location" /></label
              ><label
                >Workplace<select name="workplace" [(ngModel)]="draft.workplaceType">
                  <option value="remote">Remote</option>
                  <option value="hybrid">Hybrid</option>
                  <option value="onSite">On site</option>
                </select></label
              ><label
                >Employment<select name="employment" [(ngModel)]="draft.employmentType">
                  <option value="fullTime">Full time</option>
                  <option value="partTime">Part time</option>
                  <option value="contract">Contract</option>
                  <option value="internship">Internship</option>
                </select></label
              >
            </div>
            <label
              >Description<textarea
                required
                name="description"
                [(ngModel)]="draft.description"
              ></textarea></label
            ><label>Skills (comma separated)<input name="skills" [(ngModel)]="skillsText" /></label
            ><button class="button primary">Save draft</button>
          </form>
        }
        <section>
          <div class="section-title">
            <h2>Open roles & pipeline</h2>
            <span>{{ jobs().length }} roles</span>
          </div>
          @for (job of jobs(); track job.id) {
            <article class="panel role-card">
              <div class="role-head">
                <div>
                  <span class="pill">{{ label(job.status) }}</span>
                  <button class="role-title" type="button" (click)="toggleJob(job.id)" [attr.aria-expanded]="isExpanded(job.id)">
                    {{ job.title }}
                  </button>
                  <p>{{ job.location }} · {{ label(job.workplaceType) }}</p>
                </div>
                <div class="inline-actions">
                  @if (job.status === 'draft' || job.status === 'paused') {
                    <button class="button primary small" (click)="status(job, 'publish')">
                      Publish
                    </button>
                  }
                  @if (job.status === 'open') {
                    <button class="button ghost small" (click)="status(job, 'pause')">Pause</button>
                  }
                  @if (job.status !== 'closed') {
                    <button class="button ghost small" (click)="status(job, 'close')">Close</button>
                  }
                  <button class="button light small" type="button" (click)="loadPipeline(job)">Refresh candidates</button>
                </div>
              </div>
              @if (isExpanded(job.id)) {
                <div class="pipeline">
                  @for (app of pipelines()[job.id] ?? []; track app.id) {
                    <div class="candidate">
                      <div class="candidate-main">
                        <strong>{{ app.candidateName }}</strong>
                        <small>{{ app.candidateEmail }} · Applied {{ date(app.appliedAt) }}</small>
                        @if (profiles()[app.id]; as profile) {
                          <div class="applicant-profile">
                            @if (profile.headline) { <b>{{ profile.headline }}</b> }
                            @if (profile.summary) { <p>{{ profile.summary }}</p> }
                            @if (profile.location) { <small>{{ profile.location }}</small> }
                            <div class="cv-links">
                              @for (cv of profile.cvs; track cv.id) {
                                <a [href]="cv.url" target="_blank" rel="noopener">View CV: {{ cv.name }}</a>
                              } @empty { <small>No CV uploaded</small> }
                            </div>
                          </div>
                        } @else { <small class="profile-loading">Applicant profile unavailable.</small> }
                      </div>
                      <span class="pill stage">{{ label(app.stage) }}</span>
                      <select [ngModel]="app.stage" (ngModelChange)="advance(app, $event)">
                        <option [value]="app.stage">Move to…</option>
                        @for (stage of app.nextStages; track stage) {
                          <option [value]="stage">{{ label(stage) }}</option>
                        }
                      </select>
                    </div>
                  } @empty { <div class="empty compact">No applications yet.</div> }
                </div>
              }
            </article>
          } @empty {
            <div class="empty">Create your first role to begin hiring.</div>
          }
        </section>
      } @else {
        <div class="section-title">
          <h2>My applications</h2>
          <a routerLink="/jobs">Explore more roles →</a>
        </div>
        <section class="application-grid">
          @for (app of applications(); track app.id) {
            <article class="panel application-card">
              <span class="pill stage">{{ label(app.stage) }}</span>
              <h2>{{ app.jobTitle }}</h2>
              <p>Applied {{ date(app.appliedAt) }}</p>
              <div class="timeline">
                @for (step of stages; track step) {
                  <span [class.done]="stageIndex(step) <= stageIndex(app.stage)"></span>
                }
              </div>
              @if (!terminal(app.stage)) {
                <button class="button ghost small" (click)="withdraw(app)">
                  Withdraw application
                </button>
              }
            </article>
          } @empty {
            <div class="empty">
              You have no applications yet. <a routerLink="/jobs">Find your next role.</a>
            </div>
          }
        </section>
      }
    </main>
  `,
})
export class Dashboard implements OnInit {
  protected readonly auth = inject(AuthService);
  private readonly applicationsApi = inject(ApplicationsApi);
  private readonly jobsApi = inject(JobsApi);
  protected readonly jobs = signal<Job[]>([]);
  protected readonly applications = signal<JobApplication[]>([]);
  protected readonly pipelines = signal<Record<string, JobApplication[]>>({});
  protected readonly profiles = signal<Record<string, import('../core/models').ApplicantProfile>>({});
  protected readonly expandedJobs = signal<string[]>([]);
  protected readonly showForm = signal(false);
  protected readonly error = signal('');
  protected readonly label = enumLabel;
  protected readonly stages: ApplicationStage[] = [
    'applied',
    'screening',
    'interviewing',
    'offered',
    'hired',
  ];
  protected skillsText = '';
  protected draft: JobInput = {
    title: '',
    description: '',
    location: '',
    employmentType: 'fullTime',
    workplaceType: 'remote',
    salary: null,
    skills: [],
  };
  async ngOnInit(): Promise<void> {
    await this.reload();
  }
  private async reload(): Promise<void> {
    try {
      if (this.auth.staff()) {
        const jobs = await firstValueFrom(this.jobsApi.listForCompany());
        this.jobs.set(jobs);
        this.expandedJobs.set(jobs.map((job) => job.id));
        await Promise.all(jobs.map((job) => this.loadPipeline(job)));
      } else this.applications.set(await firstValueFrom(this.applicationsApi.listMine()));
    } catch {
      this.error.set('Could not load your workspace.');
    }
  }
  protected async createJob(): Promise<void> {
    try {
      await firstValueFrom(
        this.jobsApi.create({
          ...this.draft,
          skills: this.skillsText
            .split(',')
            .map((x) => x.trim())
            .filter(Boolean),
        }),
      );
      this.showForm.set(false);
      this.draft = {
        title: '',
        description: '',
        location: '',
        employmentType: 'fullTime',
        workplaceType: 'remote',
        salary: null,
        skills: [],
      };
      this.skillsText = '';
      await this.reload();
    } catch (error: unknown) {
      this.error.set(httpErrorMessage(error, 'Could not create role.'));
    }
  }
  protected async status(job: Job, change: 'publish' | 'pause' | 'close'): Promise<void> {
    await firstValueFrom(this.jobsApi.changeStatus(job.id, change));
    await this.reload();
  }
  protected isExpanded(jobId: string): boolean {
    return this.expandedJobs().includes(jobId);
  }
  protected toggleJob(jobId: string): void {
    this.expandedJobs.update((ids) => ids.includes(jobId) ? ids.filter((id) => id !== jobId) : [...ids, jobId]);
  }
  protected async loadPipeline(job: Job): Promise<void> {
    const applications = await firstValueFrom(this.applicationsApi.listForJob(job.id));
    this.pipelines.update((all) => ({ ...all, [job.id]: applications }));
    const loaded = await Promise.all(applications.map(async (app) => {
      try { return [app.id, await firstValueFrom(this.applicationsApi.candidateProfile(app.id))] as const; } catch { return null; }
    }));
    this.profiles.update((all) => ({ ...all, ...Object.fromEntries(loaded.filter((entry): entry is NonNullable<typeof entry> => entry !== null)) }));
  }
  protected async advance(app: JobApplication, to: ApplicationStage): Promise<void> {
    if (to === app.stage) return;
    await firstValueFrom(this.applicationsApi.advance(app.id, to, null));
    const job = this.jobs().find((x) => x.id === app.jobId);
    if (job) await this.loadPipeline(job);
  }
  protected async withdraw(app: JobApplication): Promise<void> {
    await firstValueFrom(this.applicationsApi.withdraw(app.id));
    await this.reload();
  }
  protected date(iso: string): string {
    return new Intl.DateTimeFormat(undefined, { dateStyle: 'medium' }).format(new Date(iso));
  }
  protected stageIndex(stage: ApplicationStage): number {
    return this.stages.indexOf(stage);
  }
  protected terminal(stage: ApplicationStage): boolean {
    return ['hired', 'rejected', 'withdrawn'].includes(stage);
  }
}
