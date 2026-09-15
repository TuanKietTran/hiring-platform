import type { OnInit } from '@angular/core';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { ApplicantProfileApi } from '../core/api/applicant-profile.api';
import { BlobsApi } from '../core/api/blobs.api';
import { httpErrorMessage } from '../core/http-error';
import type { ApplicantProfile, ApplicantProfileInput } from '../core/models';

@Component({
  imports: [FormsModule],
  template: `
    <main class="page profile-page">
      <div class="page-heading">
        <p class="eyebrow">APPLICANT PORTAL</p>
        <h1>Your professional profile.</h1>
        <p>Keep your story, skills, and CV versions ready for every application.</p>
      </div>
      @if (error()) {
        <div class="alert">{{ error() }}</div>
      }
      @if (notice()) {
        <p class="notice">{{ notice() }}</p>
      }
      <div class="profile-layout">
        <form class="panel profile-form" (ngSubmit)="save()">
          <h2>Profile</h2>
          <label
            >Headline<input
              name="headline"
              maxlength="160"
              [(ngModel)]="draft.headline"
              placeholder="Senior product designer"
          /></label>
          <label
            >Location<input
              name="location"
              maxlength="160"
              [(ngModel)]="draft.location"
              placeholder="London, UK"
          /></label>
          <div class="phone-fields">
            <label
              >Calling code<input
                name="phoneCallingCode"
                [(ngModel)]="draft.phoneCallingCode"
                placeholder="44" /></label
            ><label
              >Phone<input name="phone" [(ngModel)]="draft.phone" placeholder="20 1234 5678"
            /></label>
          </div>
          <label
            >Professional summary<textarea
              name="summary"
              maxlength="2000"
              [(ngModel)]="draft.summary"
            ></textarea>
          </label>
          <label
            >Skills<input
              name="skills"
              [(ngModel)]="skillsText"
              placeholder="Research, Figma, Design systems"
          /></label>
          <button class="button primary" [disabled]="saving()">
            {{ saving() ? 'Saving…' : 'Save profile' }}
          </button>
        </form>
        <section class="panel cv-panel">
          <h2>CV library</h2>
          <p class="muted-copy">
            Upload a document or store a secure HTTPS link. Your primary CV is used by default.
          </p>
          <form class="cv-add" (ngSubmit)="uploadCv()">
            <input
              required
              type="file"
              name="cvFile"
              accept=".pdf,.doc,.docx,.odt,.rtf,.txt"
              (change)="selectFile($event)"
            /><button class="button primary small" [disabled]="uploading() || !selectedFile()">
              {{ uploading() ? 'Uploading…' : 'Upload CV' }}
            </button>
          </form>
          <form class="cv-add" (ngSubmit)="addCv()">
            <input
              required
              name="cvName"
              [(ngModel)]="cvName"
              placeholder="Product CV 2026"
            /><input
              required
              type="url"
              name="cvUrl"
              [(ngModel)]="cvUrl"
              placeholder="https://…"
            /><button class="button primary small">Add CV</button>
          </form>
          <div class="cv-list">
            @for (cv of profile()?.cvs ?? []; track cv.id) {
              <article>
                <div>
                  <a [href]="cv.url" target="_blank" rel="noopener">{{ cv.name }}</a>
                  @if (cv.isPrimary) {
                    <span class="pill">Primary</span>
                  }
                  <small>Added {{ date(cv.addedAt) }}</small>
                </div>
                <div class="inline-actions">
                  @if (!cv.isPrimary) {
                    <button class="button light small" (click)="primary(cv.id)">
                      Make primary
                    </button>
                  }
                  <button class="button ghost small" (click)="remove(cv.id)">Remove</button>
                </div>
              </article>
            } @empty {
              <div class="empty compact">No CV versions yet.</div>
            }
          </div>
        </section>
      </div>
    </main>
  `,
})
export class ApplicantProfilePage implements OnInit {
  private readonly blobsApi = inject(BlobsApi);
  private readonly profileApi = inject(ApplicantProfileApi);
  protected readonly profile = signal<ApplicantProfile | null>(null);
  protected readonly saving = signal(false);
  protected readonly uploading = signal(false);
  protected readonly selectedFile = signal<File | null>(null);
  protected readonly error = signal('');
  protected readonly notice = signal('');
  protected draft: ApplicantProfileInput = {
    headline: '',
    summary: '',
    location: '',
    phone: null,
    phoneCallingCode: null,
    skills: [],
  };
  protected skillsText = '';
  protected cvName = '';
  protected cvUrl = '';
  async ngOnInit(): Promise<void> {
    try {
      this.setProfile(await firstValueFrom(this.profileApi.get()));
    } catch (e) {
      this.error.set(httpErrorMessage(e, 'Could not load profile.'));
    }
  }
  protected async save(): Promise<void> {
    this.saving.set(true);
    this.error.set('');
    try {
      this.draft.skills = this.skillsText
        .split(',')
        .map((x) => x.trim())
        .filter(Boolean);
      this.setProfile(await firstValueFrom(this.profileApi.update(this.draft)));
      this.notice.set('Profile saved.');
    } catch (e) {
      this.error.set(httpErrorMessage(e, 'Could not save profile.'));
    } finally {
      this.saving.set(false);
    }
  }
  protected selectFile(event: Event): void {
    this.selectedFile.set((event.target as HTMLInputElement).files?.[0] ?? null);
  }
  protected async uploadCv(): Promise<void> {
    const file = this.selectedFile();
    if (!file) return;
    this.uploading.set(true);
    this.error.set('');
    let blobId: string | null = null;
    try {
      const blob = await firstValueFrom(this.blobsApi.upload(file, 'candidate-cv'));
      blobId = blob.id;
      this.setProfile(
        await firstValueFrom(this.profileApi.addCv({ name: blob.name, url: blob.url })),
      );
      this.selectedFile.set(null);
      this.notice.set('CV uploaded.');
    } catch (e) {
      if (blobId) await firstValueFrom(this.blobsApi.delete(blobId)).catch(() => undefined);
      this.error.set(httpErrorMessage(e, 'Could not upload CV.'));
    } finally {
      this.uploading.set(false);
    }
  }
  protected async addCv(): Promise<void> {
    try {
      this.setProfile(
        await firstValueFrom(this.profileApi.addCv({ name: this.cvName, url: this.cvUrl })),
      );
      this.cvName = '';
      this.cvUrl = '';
    } catch (e) {
      this.error.set(httpErrorMessage(e, 'Could not add CV.'));
    }
  }
  protected async primary(id: string): Promise<void> {
    this.setProfile(await firstValueFrom(this.profileApi.setPrimaryCv(id)));
  }
  protected async remove(id: string): Promise<void> {
    const blobId = this.blobId(this.profile()?.cvs.find((cv) => cv.id === id)?.url);
    this.setProfile(await firstValueFrom(this.profileApi.removeCv(id)));
    if (blobId) await firstValueFrom(this.blobsApi.delete(blobId)).catch(() => undefined);
  }
  protected date(value: string): string {
    return new Intl.DateTimeFormat(undefined, { dateStyle: 'medium' }).format(new Date(value));
  }
  private blobId(url: string | undefined): string | null {
    return url?.match(/^\/api\/blobs\/([0-9a-f-]+)$/i)?.[1] ?? null;
  }
  private setProfile(profile: ApplicantProfile): void {
    this.profile.set(profile);
    this.draft = {
      headline: profile.headline,
      summary: profile.summary,
      location: profile.location,
      phone: profile.phone,
      phoneCallingCode: profile.phoneCallingCode,
      skills: [...profile.skills],
    };
    this.skillsText = profile.skills.join(', ');
  }
}
