import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Api } from './api';
import { Admin } from './admin';

describe('Administration', () => {
  const user = { id: 'other', email: 'disposable@example.test', isEnabled: true };
  async function setup() {
    const api = {
      session: signal({ id: 'self', isAdmin: true }),
      get: vi.fn(async (path: string) =>
        path.endsWith('/users')
          ? [user, { ...user, id: 'self', email: 'admin@example.test' }]
          : [{ id: 1, displayName: 'Email', isEnabled: true }],
      ),
      mutate: vi.fn().mockResolvedValue(undefined),
    };
    TestBed.configureTestingModule({ providers: [{ provide: Api, useValue: api }] });
    const fixture = TestBed.createComponent(Admin);
    fixture.detectChanges();
    await vi.waitFor(() => expect(fixture.componentInstance.ready()).toBe(true));
    fixture.detectChanges();
    return { fixture, component: fixture.componentInstance, api };
  }
  it('requires explicit confirmation and supports cancellation before deletion', async () => {
    const { fixture, component, api } = await setup();
    await component.remove(user);
    expect(api.mutate).not.toHaveBeenCalled();
    const buttons = Array.from(
      fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLButtonElement>,
    );
    buttons.find((x) => x.textContent?.includes('Delete disposable'))!.click();
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[role=alertdialog]').textContent).toContain(
      'cannot be undone',
    );
    expect(api.mutate).not.toHaveBeenCalled();
    (Array.from(fixture.nativeElement.querySelectorAll('button')) as HTMLButtonElement[])
      .find((x) => x.textContent === 'Cancel')!
      .click();
    expect(component.deleting()).toBeNull();
    component.deleting.set(user);
    await component.remove(user);
    expect(api.mutate).toHaveBeenCalledWith('DELETE', '/admin/users/other', null);
  });
  it('disables self-management actions and uses explicit global enablement values', async () => {
    const { fixture, component, api } = await setup();
    const buttons = Array.from(
      fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLButtonElement>,
    );
    expect(
      buttons.filter((x) => x.textContent?.includes('admin@example.test')).every((x) => x.disabled),
    ).toBe(true);
    await component.toggleCatalog('channels', {
      id: 1,
      code: 'email',
      displayName: 'Email',
      isEnabled: true,
    });
    expect(api.mutate).toHaveBeenCalledWith('PUT', '/admin/channels/1/enabled', {
      isEnabled: false,
    });
  });
});
