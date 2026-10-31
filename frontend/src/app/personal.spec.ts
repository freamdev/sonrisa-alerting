import { TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { Api } from './api';
import { Personal } from './personal';

describe('Independent personal choices', () => {
  const topics = [
    { id: 1, code: 'finance', displayName: 'Finance', isEnabled: true },
    { id: 3, code: 'weather', displayName: 'Weather', isEnabled: false },
  ];
  const channels = [
    { id: 1, code: 'email', displayName: 'Email', isEnabled: true },
    { id: 2, code: 'slack', displayName: 'Slack', isEnabled: false },
  ];
  const setup = async () => {
    const api = {
      get: vi.fn(async (path: string) =>
        path.endsWith('/topics')
          ? topics
          : path.endsWith('/channels')
            ? channels
            : { topicIds: [1, 3], channelIds: [1] },
      ),
      mutate: vi.fn().mockResolvedValue(undefined),
    };
    TestBed.configureTestingModule({ providers: [{ provide: Api, useValue: api }] });
    const fixture = TestBed.createComponent(Personal);
    fixture.detectChanges();
    await vi.waitFor(() => expect(fixture.componentInstance.ready()).toBe(true));
    fixture.detectChanges();
    return { fixture, component: fixture.componentInstance, api };
  };
  it('renders two separate checkbox groups and permits removal of disabled selected items', async () => {
    const { fixture } = await setup();
    const root: HTMLElement = fixture.nativeElement;
    expect(Array.from(root.querySelectorAll('legend')).map((x) => x.textContent)).toEqual([
      'Topics',
      'Receive via',
    ]);
    const inputs = root.querySelectorAll<HTMLInputElement>('input');
    expect(inputs.length).toBe(4);
    expect(inputs[1].checked).toBe(true);
    expect(inputs[1].disabled).toBe(false);
    expect(inputs[3].disabled).toBe(true);
    expect(root.textContent).toContain('globally disabled');
  });
  it('removes Slack without altering topics or Email', async () => {
    const { component, api } = await setup();
    component.selections.set({ topicIds: [1, 3], channelIds: [1, 2] });
    const input = document.createElement('input');
    input.checked = false;
    await component.change('channels', 2, { target: input } as unknown as Event);
    expect(api.mutate).toHaveBeenCalledWith('DELETE', '/me/channels/2');
    expect(component.selections()).toEqual({ topicIds: [1, 3], channelIds: [1] });
    expect(component.status()).toBe('Saved.');
  });
  it('adds a topic without altering channels', async () => {
    const { component, api } = await setup();
    component.selections.set({ topicIds: [], channelIds: [1, 2] });
    const input = document.createElement('input');
    input.checked = true;
    await component.change('topics', 1, { target: input } as unknown as Event);
    expect(api.mutate).toHaveBeenCalledWith('PUT', '/me/topics/1');
    expect(component.selections()).toEqual({ topicIds: [1], channelIds: [1, 2] });
  });
  it('restores the checkbox and announces a failed write', async () => {
    const { component, api, fixture } = await setup();
    api.mutate.mockRejectedValue(new HttpErrorResponse({ status: 403 }));
    const input = document.createElement('input');
    input.checked = false;
    await component.change('topics', 1, { target: input } as unknown as Event);
    fixture.detectChanges();
    expect(input.checked).toBe(true);
    expect(component.selections().topicIds).toEqual([1, 3]);
    expect(fixture.nativeElement.querySelector('[role=alert]').textContent).toContain('permission');
  });
});
