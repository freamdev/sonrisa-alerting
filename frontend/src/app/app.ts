import { Component, inject, signal } from '@angular/core';
import { Router, RouterLink, RouterOutlet } from '@angular/router';
import { Api, errorMessage } from './api';

@Component({
  imports: [RouterLink, RouterOutlet],
  selector: 'app-root',
  template: `
    <header>
      <h1>Alerts</h1>
      <nav aria-label="Main navigation">
        <a routerLink="/login">Sign in</a>
        @if (api.session(); as session) {
          <a routerLink="/personal">Personal settings</a>
          @if (session.isAdmin) {
            <a routerLink="/admin">Administration</a>
          }
          <span>{{ session.email }} ({{ session.isAdmin ? 'Admin' : 'User' }})</span>
          <button (click)="logout()" [disabled]="busy()">Sign out</button>
        }
      </nav>
      @if (error()) {
        <p role="alert">{{ error() }}</p>
      }
    </header>
    <main><router-outlet /></main>
  `,
})
export class App {
  readonly api = inject(Api);
  private readonly router = inject(Router);
  readonly error = signal('');
  readonly busy = signal(false);
  async logout(): Promise<void> {
    if (this.busy()) return;
    this.busy.set(true);
    this.error.set('');
    try {
      await this.api.mutate('POST', '/auth/logout');
      this.api.session.set(null);
      await this.router.navigateByUrl('/login');
    } catch (error) {
      this.error.set(errorMessage(error));
    } finally {
      this.busy.set(false);
    }
  }
}
