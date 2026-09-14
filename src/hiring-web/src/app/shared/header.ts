import { Component, inject } from '@angular/core';
import { Router, RouterLink, RouterLinkActive } from '@angular/router';
import { AuthService } from '../core/auth.service';

@Component({
  selector: 'app-header',
  imports: [RouterLink, RouterLinkActive],
  template: `
    <header class="topbar">
      <a class="brand" routerLink="/"><span>H</span> Hirelane</a>
      <nav>
        <a routerLink="/jobs" routerLinkActive="active">Find jobs</a>
        @if (auth.authenticated()) { <a routerLink="/dashboard" routerLinkActive="active">Dashboard</a> }
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
    </header>
  `,
})
export class Header {
  protected readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  protected async logout(): Promise<void> { await this.auth.logout(); await this.router.navigateByUrl('/'); }
}
