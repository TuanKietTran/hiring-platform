import { Component, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink, RouterLinkActive } from '@angular/router';
import { filter } from 'rxjs';
import { AuthService } from '../core/auth.service';

@Component({
  selector: 'app-header',
  imports: [RouterLink, RouterLinkActive],
  template: `
    <header class="topbar" [class.menu-open]="menuOpen()">
      <a class="brand" routerLink="/"><span>H</span> Hirelane</a>
      <nav id="primary-nav" aria-label="Primary">
        <a routerLink="/jobs" routerLinkActive="active">Find jobs</a>
        @if (auth.authenticated()) {
          <a routerLink="/dashboard" routerLinkActive="active">Dashboard</a>
        }
        @if (auth.user()?.role === 'candidate') {
          <a routerLink="/profile" routerLinkActive="active">Profile & CV</a>
        }
      </nav>
      <div class="account">
        @if (auth.user(); as user) {
          <span class="user-name">{{ user.fullName }}</span>
          <button class="button ghost small" (click)="logout()">Sign out</button>
        } @else {
          <a class="button ghost small" routerLink="/login">Sign in</a>
          <a class="button primary small" routerLink="/register">Join now</a>
        }
      </div>
      <button
        class="menu-toggle"
        type="button"
        aria-controls="primary-nav"
        [attr.aria-expanded]="menuOpen()"
        [attr.aria-label]="menuOpen() ? 'Close menu' : 'Open menu'"
        (click)="menuOpen.set(!menuOpen())"
      >
        <span></span><span></span><span></span>
      </button>
    </header>
  `,
  host: { '(document:keydown.escape)': 'menuOpen.set(false)' },
})
export class Header {
  protected readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  protected readonly menuOpen = signal(false);
  constructor() {
    this.router.events
      .pipe(
        filter((event) => event instanceof NavigationEnd),
        takeUntilDestroyed(),
      )
      .subscribe(() => this.menuOpen.set(false));
  }
  protected async logout(): Promise<void> {
    this.menuOpen.set(false);
    await this.auth.logout();
    await this.router.navigateByUrl('/');
  }
}
