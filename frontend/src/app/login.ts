import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { Api, errorMessage } from './api';
@Component({
  selector: 'app-login',
  imports: [FormsModule],
  template: `
    <h2>Sign in</h2>
    <p>Use the account supplied by your administrator.</p>
    <form (ngSubmit)="login()" #form="ngForm">
      <label
        >Email
        <input name="email" type="email" autocomplete="username" required [(ngModel)]="email"
      /></label>
      <label
        >Password
        <input
          name="password"
          type="password"
          autocomplete="current-password"
          required
          [(ngModel)]="password"
      /></label>
      <button [disabled]="busy() || form.invalid">{{ busy() ? 'Signing in…' : 'Sign in' }}</button>
    </form>
    @if (error()) {
      <p role="alert">{{ error() }}</p>
    }
  `,
})
export class Login {
  private readonly api = inject(Api);
  private readonly router = inject(Router);
  email = '';
  password = '';
  readonly busy = signal(false);
  readonly error = signal('');
  async login(): Promise<void> {
    if (this.busy()) return;
    this.busy.set(true);
    this.error.set('');
    try {
      await this.api.mutate('POST', '/auth/login', { email: this.email, password: this.password });
      this.password = '';
      await this.api.current();
      await this.router.navigateByUrl('/personal');
    } catch (error) {
      this.error.set(errorMessage(error));
    } finally {
      this.password = '';
      this.busy.set(false);
    }
  }
}
