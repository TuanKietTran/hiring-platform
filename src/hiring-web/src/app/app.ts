import { Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { AuthService } from './core/auth.service';
import { ConnectivityService } from './core/connectivity.service';
import { Header } from './shared/header';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, Header],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {
  protected readonly auth = inject(AuthService);
  protected readonly connectivity = inject(ConnectivityService);
  constructor() {
    this.connectivity.start();
    void this.auth.restore();
  }
}
