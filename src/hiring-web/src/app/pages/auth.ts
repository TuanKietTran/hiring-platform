import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { AuthApi } from '../core/api/auth.api';
import { AuthService } from '../core/auth.service';
import { httpErrorMessage } from '../core/http-error';

@Component({
  imports: [FormsModule, RouterLink],
  template: `<main class="auth-page">
    <form class="auth-card" (ngSubmit)="submit()">
      <p class="eyebrow">WELCOME BACK</p>
      <h1>Sign in to Hirelane</h1>
      <p>Keep your next move moving.</p>
      <label
        >Email<input
          required
          type="email"
          name="email"
          [(ngModel)]="email"
          autocomplete="email" /></label
      ><label
        >Password<input
          required
          type="password"
          name="password"
          [(ngModel)]="password"
          autocomplete="current-password"
      /></label>
      @if (error()) {
        <div class="alert">{{ error() }}</div>
      }
      <button class="button primary wide" [disabled]="busy()">
        {{ busy() ? 'Signing in…' : 'Sign in' }}</button
      ><small>New here? <a routerLink="/register">Create an account</a></small>
    </form>
  </main>`,
})
export class LoginPage {
  private readonly api = inject(AuthApi);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  protected email = '';
  protected password = '';
  protected readonly busy = signal(false);
  protected readonly error = signal('');
  protected async submit(): Promise<void> {
    this.busy.set(true);
    this.error.set('');
    try {
      const user = await firstValueFrom(
        this.api.login({ email: this.email, password: this.password }),
      );
      this.auth.setUser(user);
      await this.router.navigateByUrl('/dashboard');
    } catch {
      this.error.set('Email or password is incorrect.');
    } finally {
      this.busy.set(false);
    }
  }
}

@Component({
  imports: [FormsModule, RouterLink],
  template: `<main class="auth-page">
    <form class="auth-card" (ngSubmit)="submit()">
      <p class="eyebrow">JOIN HIRELANE</p>
      <h1>Create your account</h1>
      <div class="segmented">
        <button type="button" [class.active]="kind === 'candidate'" (click)="kind = 'candidate'">
          Find work</button
        ><button type="button" [class.active]="kind === 'company'" (click)="kind = 'company'">
          Build a team
        </button>
      </div>
      @if (kind === 'company') {
        <label>Company name<input required name="company" [(ngModel)]="companyName" /></label>
      }
      <label
        >Your name<input required name="name" [(ngModel)]="fullName" autocomplete="name" /></label
      ><label
        >Email<input
          required
          type="email"
          name="email"
          [(ngModel)]="email"
          autocomplete="email" /></label
      ><label
        >Password<input
          required
          minlength="10"
          type="password"
          name="password"
          [(ngModel)]="password"
          autocomplete="new-password"
        /><small>10+ characters, one uppercase letter and one number.</small></label
      >
      @if (error()) {
        <div class="alert">{{ error() }}</div>
      }
      <button class="button primary wide" [disabled]="busy()">
        {{ busy() ? 'Creating…' : 'Create account' }}</button
      ><small>Already a member? <a routerLink="/login">Sign in</a></small>
    </form>
  </main>`,
})
export class RegisterPage {
  private readonly api = inject(AuthApi);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  protected kind: 'candidate' | 'company' = 'candidate';
  protected companyName = '';
  protected fullName = '';
  protected email = '';
  protected password = '';
  protected readonly busy = signal(false);
  protected readonly error = signal('');
  protected async submit(): Promise<void> {
    this.busy.set(true);
    this.error.set('');
    try {
      const user =
        this.kind === 'candidate'
          ? await firstValueFrom(
              this.api.registerCandidate({
                email: this.email,
                password: this.password,
                fullName: this.fullName,
              }),
            )
          : await firstValueFrom(
              this.api.registerCompany({
                companyName: this.companyName,
                website: null,
                email: this.email,
                password: this.password,
                fullName: this.fullName,
              }),
            );
      this.auth.setUser(user);
      await this.router.navigateByUrl('/dashboard');
    } catch (error: unknown) {
      this.error.set(httpErrorMessage(error, 'Could not create your account.'));
    } finally {
      this.busy.set(false);
    }
  }
}
