import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { provideRouter, Router } from '@angular/router';
import { Api } from './api';
import { App } from './app';
import { Login } from './login';

describe('Login and logout', () => {
  function setup() {
    const api = {
      session: signal({ id: 'u', email: 'user', isAdmin: false }),
      mutate: vi.fn().mockResolvedValue(undefined),
      current: vi.fn().mockResolvedValue({ id: 'u' }),
    };
    TestBed.configureTestingModule({
      providers: [provideRouter([]), { provide: Api, useValue: api }],
    });
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
    return { api, navigate };
  }
  it('refreshes current identity after login, clears password and opens personal settings', async () => {
    const { api, navigate } = setup();
    const component = TestBed.createComponent(Login).componentInstance;
    component.email = 'user@example.test';
    component.password = 'test-only-password';
    await component.login();
    expect(api.mutate).toHaveBeenCalledWith('POST', '/auth/login', {
      email: 'user@example.test',
      password: 'test-only-password',
    });
    expect(api.current).toHaveBeenCalled();
    expect(component.password).toBe('');
    expect(navigate).toHaveBeenCalledWith('/personal');
  });
  it('announces network failure and clears the password without navigating', async () => {
    const { api, navigate } = setup();
    api.mutate.mockRejectedValue(new HttpErrorResponse({ status: 0 }));
    const fixture = TestBed.createComponent(Login);
    fixture.componentInstance.password = 'test-only-password';
    await fixture.componentInstance.login();
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[role=alert]').textContent).toContain(
      'Cannot reach',
    );
    expect(fixture.componentInstance.password).toBe('');
    expect(navigate).not.toHaveBeenCalled();
  });
  it('clears current identity only after successful logout', async () => {
    const { api, navigate } = setup();
    const component = TestBed.createComponent(App).componentInstance;
    await component.logout();
    expect(api.mutate).toHaveBeenCalledWith('POST', '/auth/logout');
    expect(api.session()).toBeNull();
    expect(navigate).toHaveBeenCalledWith('/login');
  });
});
