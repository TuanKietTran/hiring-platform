import type { OnInit } from '@angular/core';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ApplicationsApi } from '../core/api/applications.api';
import { JobsApi } from '../core/api/jobs.api';
import { AuthService } from '../core/auth.service';
import { httpErrorMessage } from '../core/http-error';
import type { Job } from '../core/models';
import { enumLabel } from '../core/models';

@Component({
  imports: [FormsModule, RouterLink],
  template: `
    <main class="page narrow">
      <a class="back" routerLink="/jobs">← All opportunities</a>
      @if (job(); as item) {
        <article class="detail">
          <div class="detail-top">
            <div>
              <p class="eyebrow">{{ item.companyName }}</p>
              <h1>{{ item.title }}</h1>
              <p>
                {{ item.location || 'Location flexible' }} · {{ label(item.workplaceType) }} ·
                {{ label(item.employmentType) }}
              </p>
            </div>
            <span class="pill">{{ label(item.status) }}</span>
          </div>
          <div class="skill-row">
            @for (skill of item.skills; track skill) {
              <span>{{ skill }}</span>
            }
          </div>
          <hr />
          <h2>About the role</h2>
          <p class="description">{{ item.description }}</p>
        </article>
        @if (auth.user()?.role === 'candidate') {
          <section class="panel apply">
            <h2>Interested?</h2>
            <p>Share a short note with the hiring team.</p>
            <textarea
              [(ngModel)]="coverLetter"
              placeholder="Why does this role speak to you?"
            ></textarea
            ><input [(ngModel)]="resumeUrl" placeholder="HTTPS resume URL (optional)" /><button
              class="button primary"
              [disabled]="sending()"
              (click)="apply(item.id)"
            >
              {{ sending() ? 'Submitting…' : 'Apply now' }}
            </button>
            @if (message()) {
              <p class="notice">{{ message() }}</p>
            }
          </section>
        } @else if (!auth.authenticated()) {
          <section class="panel">
            <h2>Ready to apply?</h2>
            <a class="button primary" routerLink="/login">Sign in to continue</a>
          </section>
        }
      } @else if (error()) {
        <div class="alert">{{ error() }}</div>
      }
    </main>
  `,
})
export class JobDetail implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly applicationsApi = inject(ApplicationsApi);
  private readonly jobsApi = inject(JobsApi);
  protected readonly auth = inject(AuthService);
  protected readonly job = signal<Job | null>(null);
  protected readonly error = signal('');
  protected readonly sending = signal(false);
  protected readonly message = signal('');
  protected coverLetter = '';
  protected resumeUrl = '';
  protected readonly label = enumLabel;
  async ngOnInit(): Promise<void> {
    try {
      this.job.set(await firstValueFrom(this.jobsApi.get(this.route.snapshot.paramMap.get('id')!)));
    } catch {
      this.error.set('This role could not be found.');
    }
  }
  protected async apply(id: string): Promise<void> {
    this.sending.set(true);
    try {
      await firstValueFrom(
        this.applicationsApi.apply(id, this.coverLetter, this.resumeUrl || null),
      );
      this.message.set('Application submitted. Good luck!');
    } catch (error: unknown) {
      this.message.set(httpErrorMessage(error, 'Could not submit application.'));
    } finally {
      this.sending.set(false);
    }
  }
}
