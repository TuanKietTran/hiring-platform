import type { OnInit } from '@angular/core';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { JobsApi } from '../core/api/jobs.api';
import type { Job, WorkplaceType } from '../core/models';
import { enumLabel } from '../core/models';

@Component({
  imports: [FormsModule, RouterLink],
  template: `
    <main class="page">
      <div class="page-heading">
        <p class="eyebrow">OPPORTUNITIES</p>
        <h1>Find work worth doing.</h1>
        <p>Explore roles from teams who care about craft, clarity, and people.</p>
      </div>
      <form class="searchbar" (ngSubmit)="load()">
        <input name="q" [(ngModel)]="query" placeholder="Search title, skill, or location" /><select
          name="workplace"
          [(ngModel)]="workplace"
        >
          <option value="">All workplaces</option>
          <option value="remote">Remote</option>
          <option value="hybrid">Hybrid</option>
          <option value="onSite">On site</option></select
        ><button class="button primary">Search</button>
      </form>
      @if (loading()) {
        <div class="empty">Finding the best roles…</div>
      } @else if (error()) {
        <div class="alert">{{ error() }}</div>
      } @else {
        <div class="results-head">
          <b>{{ jobs().length }} roles</b><span>Fresh opportunities, ordered by newest</span>
        </div>
        <section class="job-list">
          @for (job of jobs(); track job.id) {
            <a class="job-row" [routerLink]="['/jobs', job.id]">
              <div class="logo">{{ initials(job.companyName) }}</div>
              <div class="job-main">
                <h2>{{ job.title }}</h2>
                <p>{{ job.companyName }} · {{ job.location || 'Location flexible' }}</p>
                <div class="skill-row">
                  @for (skill of job.skills.slice(0, 4); track skill) {
                    <span>{{ skill }}</span>
                  }
                </div>
              </div>
              <div class="job-meta">
                <span class="pill">{{ label(job.workplaceType) }}</span
                ><strong>{{ salary(job) }}</strong
                ><small>{{ relative(job.publishedAt || job.createdAt) }}</small>
              </div>
            </a>
          } @empty {
            <div class="empty">No roles match those filters yet.</div>
          }
        </section>
      }
    </main>
  `,
})
export class Jobs implements OnInit {
  private readonly api = inject(JobsApi);
  protected readonly jobs = signal<Job[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal('');
  protected query = '';
  protected workplace: WorkplaceType | '' = '';
  protected readonly label = enumLabel;
  ngOnInit(): void {
    void this.load();
  }
  protected async load(): Promise<void> {
    this.loading.set(true);
    this.error.set('');
    try {
      this.jobs.set((await firstValueFrom(this.api.search(this.query, this.workplace))).items);
    } catch {
      this.error.set('Could not load jobs. Please try again.');
    } finally {
      this.loading.set(false);
    }
  }
  protected initials(name: string): string {
    return name
      .split(/\s+/)
      .map((x) => x[0])
      .join('')
      .slice(0, 2)
      .toUpperCase();
  }
  protected salary(job: Job): string {
    if (!job.salary) return 'Salary disclosed later';
    const d = job.salary.currency === 'JPY' || job.salary.currency === 'VND' ? 1 : 100;
    return `${job.salary.currency} ${(job.salary.minMinor / d).toLocaleString()}–${(job.salary.maxMinor / d).toLocaleString()}`;
  }
  protected relative(iso: string): string {
    const days = Math.max(0, Math.floor((Date.now() - Date.parse(iso)) / 86400000));
    return days === 0 ? 'Today' : `${days}d ago`;
  }
}
