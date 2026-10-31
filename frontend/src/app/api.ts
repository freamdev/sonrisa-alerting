import { inject, Injectable, signal } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';

export interface Session {
  id: string;
  email: string;
  isAdmin: boolean;
}
export interface CatalogItem {
  id: number;
  code: string;
  displayName: string;
  isEnabled: boolean;
}
export interface User {
  id: string;
  email: string;
  isEnabled: boolean;
}
export interface Selections {
  topicIds: number[];
  channelIds: number[];
}
export type Catalog = 'topics' | 'channels';

@Injectable({ providedIn: 'root' })
export class Api {
  private readonly http = inject(HttpClient);
  readonly session = signal<Session | null>(null);
  async get<T>(path: string): Promise<T> {
    try {
      return await firstValueFrom(this.http.get<T>('/api' + path));
    } catch (error) {
      this.checkSession(error);
      throw error;
    }
  }
  async mutate(
    method: 'POST' | 'PUT' | 'DELETE',
    path: string,
    body: unknown = null,
  ): Promise<void> {
    // Tokens are identity-bound: obtain a fresh token for every mutation.
    const { token } = await this.get<{ token: string }>('/auth/csrf');
    try {
      await firstValueFrom(
        this.http.request(method, '/api' + path, {
          body,
          headers: { 'X-CSRF-TOKEN': token },
          responseType: 'text',
        }),
      );
    } catch (error) {
      this.checkSession(error);
      throw error;
    }
  }
  private checkSession(error: unknown): void {
    if (error instanceof HttpErrorResponse && error.status === 401) this.session.set(null);
  }
  async current(): Promise<Session | null> {
    try {
      const session = await this.get<Session>('/auth/me');
      this.session.set(session);
      return session;
    } catch (error) {
      if (error instanceof HttpErrorResponse && error.status === 401) {
        this.session.set(null);
        return null;
      }
      throw error;
    }
  }
}
export function errorMessage(error: unknown): string {
  if (!(error instanceof HttpErrorResponse)) return 'Something went wrong. Please try again.';
  switch (error.status) {
    case 0:
      return 'Cannot reach the server. Check your connection and try again.';
    case 401:
      return 'Sign-in failed or your session expired. Sign in again with an enabled account.';
    case 403:
      return 'You do not have permission to perform this action.';
    case 400:
      return 'The request was rejected. Refresh the page and check your input.';
    case 404:
      return 'This item no longer exists. Refresh the page.';
    case 409:
      return 'This change is unavailable. The item may be disabled or your own account is protected. Refresh the page.';
    default:
      return 'The server could not complete the request. Please try again.';
  }
}
