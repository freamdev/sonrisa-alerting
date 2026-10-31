import { Component, inject, OnInit, signal } from '@angular/core';
import { Api, Catalog, CatalogItem, errorMessage, Selections } from './api';
@Component({
  selector: 'app-personal',
  template: `
    <h2>Personal settings</h2>
    <p>
      Each selected topic is sent through all your enabled delivery channels. Changes save
      immediately.
    </p>
    <p>
      Choose no topics or no channels to stop deliveries. Globally disabled items do not send; you
      can remove an existing choice.
    </p>
    @if (error()) {
      <p role="alert">{{ error() }}</p>
      <button (click)="load()" [disabled]="busy()">Reload settings</button>
    }
    <p role="status" aria-live="polite">{{ status() }}</p>
    @if (ready()) {
      @for (group of groups; track group.key) {
        <fieldset [disabled]="busy()">
          <legend>{{ group.label }}</legend>
          @for (item of catalogs()[group.key]; track item.id) {
            <label
              ><input
                type="checkbox"
                [checked]="selected(group.key, item.id)"
                [disabled]="!item.isEnabled && !selected(group.key, item.id)"
                (change)="change(group.key, item.id, $event)"
              />
              {{ item.displayName }}
              @if (!item.isEnabled) {
                <span>— globally disabled</span>
              }
            </label>
          }
        </fieldset>
      }
    }
  `,
})
export class Personal implements OnInit {
  private readonly api = inject(Api);
  readonly groups: { key: Catalog; label: string }[] = [
    { key: 'topics', label: 'Topics' },
    { key: 'channels', label: 'Receive via' },
  ];
  readonly catalogs = signal<Record<Catalog, CatalogItem[]>>({ topics: [], channels: [] });
  readonly selections = signal<Selections>({ topicIds: [], channelIds: [] });
  readonly busy = signal(false);
  readonly ready = signal(false);
  readonly status = signal('');
  readonly error = signal('');
  ngOnInit(): void {
    void this.load();
  }
  selected(kind: Catalog, id: number): boolean {
    return this.selections()[kind === 'topics' ? 'topicIds' : 'channelIds'].includes(id);
  }
  async load(): Promise<void> {
    this.busy.set(true);
    this.error.set('');
    this.status.set('Loading settings…');
    this.ready.set(false);
    try {
      const [topics, channels, selections] = await Promise.all([
        this.api.get<CatalogItem[]>('/me/topics'),
        this.api.get<CatalogItem[]>('/me/channels'),
        this.api.get<Selections>('/me/selections'),
      ]);
      this.catalogs.set({ topics, channels });
      this.selections.set(selections);
      this.ready.set(true);
      this.status.set('Settings loaded.');
    } catch (error) {
      this.status.set('');
      this.error.set(errorMessage(error));
    } finally {
      this.busy.set(false);
    }
  }
  async change(kind: Catalog, id: number, event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const checked = input.checked;
    input.checked = this.selected(kind, id);
    if (this.busy()) return;
    this.busy.set(true);
    this.error.set('');
    this.status.set('Saving…');
    try {
      await this.api.mutate(checked ? 'PUT' : 'DELETE', `/me/${kind}/${id}`);
      const key = kind === 'topics' ? 'topicIds' : 'channelIds';
      this.selections.update((value) => ({
        ...value,
        [key]: checked ? [...new Set([...value[key], id])] : value[key].filter((x) => x !== id),
      }));
      input.checked = checked;
      this.status.set('Saved.');
    } catch (error) {
      this.status.set('Change was not confirmed. Reload settings before retrying.');
      this.error.set(errorMessage(error));
    } finally {
      this.busy.set(false);
    }
  }
}
