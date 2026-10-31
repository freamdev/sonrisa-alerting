import { Component, inject, OnInit, signal } from '@angular/core';
import { Api, Catalog, CatalogItem, errorMessage, User } from './api';
@Component({
  selector: 'app-admin',
  template: `
    <h2>Administration</h2>
    <p>
      Global disablement stops deliveries while preserving personal choices. Enabling never
      subscribes anyone.
    </p>
    <button (click)="load()" [disabled]="busy()">Refresh lists</button>
    @if (error()) {
      <p role="alert">{{ error() }}</p>
    }
    <p role="status" aria-live="polite">{{ status() }}</p>
    @if (ready()) {
      <h3>Users</h3>
      <ul>
        @for (user of users(); track user.id) {
          <li>
            {{ user.email }} — {{ user.isEnabled ? 'enabled' : 'disabled' }}
            @if (user.id === api.session()?.id) {
              <span> (your account)</span>
            }
            <button [disabled]="busy() || user.id === api.session()?.id" (click)="toggleUser(user)">
              {{ user.isEnabled ? 'Disable' : 'Enable' }} {{ user.email }}
            </button>
            <button
              [disabled]="busy() || user.id === api.session()?.id"
              (click)="deleting.set(user)"
            >
              Delete {{ user.email }}
            </button>
          </li>
        }
      </ul>
      @if (deleting(); as user) {
        <section
          role="alertdialog"
          aria-labelledby="delete-title"
          aria-describedby="delete-description"
        >
          <h3 id="delete-title">Delete {{ user.email }}?</h3>
          <p id="delete-description">
            Permanently removes this account, its choices and delivery records. This cannot be
            undone.
          </p>
          <button [disabled]="busy()" (click)="remove(user)">Confirm permanent deletion</button>
          <button [disabled]="busy()" (click)="deleting.set(null)">Cancel</button>
        </section>
      }
      @for (kind of kinds; track kind) {
        <h3>{{ kind === 'topics' ? 'Topics' : 'Channels' }}</h3>
        <ul>
          @for (item of catalogs()[kind]; track item.id) {
            <li>
              {{ item.displayName }} — {{ item.isEnabled ? 'enabled' : 'disabled' }}
              <button [disabled]="busy()" (click)="toggleCatalog(kind, item)">
                {{ item.isEnabled ? 'Disable' : 'Enable' }} {{ item.displayName }} globally
              </button>
            </li>
          }
        </ul>
      }
    }
  `,
})
export class Admin implements OnInit {
  readonly api = inject(Api);
  readonly kinds: Catalog[] = ['topics', 'channels'];
  readonly users = signal<User[]>([]);
  readonly catalogs = signal<Record<Catalog, CatalogItem[]>>({ topics: [], channels: [] });
  readonly deleting = signal<User | null>(null);
  readonly busy = signal(false);
  readonly ready = signal(false);
  readonly error = signal('');
  readonly status = signal('');
  ngOnInit(): void {
    void this.load();
  }
  async load(): Promise<void> {
    this.busy.set(true);
    this.ready.set(false);
    this.error.set('');
    this.status.set('Loading…');
    this.deleting.set(null);
    try {
      const [users, topics, channels] = await Promise.all([
        this.api.get<User[]>('/admin/users'),
        this.api.get<CatalogItem[]>('/admin/topics'),
        this.api.get<CatalogItem[]>('/admin/channels'),
      ]);
      this.users.set(users);
      this.catalogs.set({ topics, channels });
      this.ready.set(true);
      this.status.set('Lists loaded.');
    } catch (error) {
      this.status.set('');
      this.error.set(errorMessage(error));
    } finally {
      this.busy.set(false);
    }
  }
  async toggleUser(user: User): Promise<void> {
    await this.perform('PUT', `/admin/users/${encodeURIComponent(user.id)}/enabled`, {
      isEnabled: !user.isEnabled,
    });
  }
  async toggleCatalog(kind: Catalog, item: CatalogItem): Promise<void> {
    await this.perform('PUT', `/admin/${kind}/${item.id}/enabled`, { isEnabled: !item.isEnabled });
  }
  async remove(user: User): Promise<void> {
    if (this.deleting()?.id !== user.id) return;
    await this.perform('DELETE', `/admin/users/${encodeURIComponent(user.id)}`);
  }
  private async perform(
    method: 'PUT' | 'DELETE',
    path: string,
    body: unknown = null,
  ): Promise<void> {
    if (this.busy()) return;
    this.busy.set(true);
    this.error.set('');
    this.status.set('Saving…');
    try {
      await this.api.mutate(method, path, body);
      await this.load();
      if (!this.error()) this.status.set('Saved.');
    } catch (error) {
      this.status.set('Change was not confirmed. Refresh lists before retrying.');
      this.error.set(errorMessage(error));
    } finally {
      this.busy.set(false);
    }
  }
}
