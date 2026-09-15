import { Injectable, signal } from '@angular/core';

export type ConnectionState = 'checking' | 'connected' | 'disconnected';

const HEALTH_URL = '/health';
const CHECK_INTERVAL_MS = 15_000;
const CHECK_TIMEOUT_MS = 5_000;
const OFFLINE_MESSAGE = 'Your device appears to be offline.';
const SERVER_MESSAGE = 'The Hirelane server is unreachable. Check your connection or restart the local services.';

@Injectable({ providedIn: 'root' })
export class ConnectivityService {
  readonly state = signal<ConnectionState>('checking');
  readonly message = signal('Checking server connection…');
  private started = false;

  start(): void {
    if (this.started) return;
    this.started = true;

    window.addEventListener('offline', this.onOffline);
    window.addEventListener('online', this.onOnline);
    window.setInterval(() => void this.check(), CHECK_INTERVAL_MS);
    void this.check();
  }

  async check(): Promise<void> {
    if (!navigator.onLine) {
      this.markDisconnected(OFFLINE_MESSAGE);
      return;
    }

    try {
      const response = await fetch(HEALTH_URL, {
        cache: 'no-store',
        credentials: 'same-origin',
        signal: AbortSignal.timeout(CHECK_TIMEOUT_MS),
      });
      if (!response.ok) throw new Error(`Health check returned ${response.status}`);
      this.markConnected();
    } catch {
      this.markDisconnected(SERVER_MESSAGE);
    }
  }

  markConnected(): void {
    this.state.set('connected');
    this.message.set('');
  }

  markRequestFailure(status: number): void {
    if (status === 0 || status === 502 || status === 503 || status === 504)
      this.markDisconnected(navigator.onLine ? SERVER_MESSAGE : OFFLINE_MESSAGE);
  }

  private readonly onOffline = (): void => this.markDisconnected(OFFLINE_MESSAGE);
  private readonly onOnline = (): void => { this.state.set('checking'); this.message.set('Reconnecting to Hirelane…'); void this.check(); };
  private markDisconnected(message: string): void { this.state.set('disconnected'); this.message.set(message); }
}
