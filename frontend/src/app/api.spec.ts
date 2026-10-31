import { TestBed } from '@angular/core/testing';
import { HttpErrorResponse, provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Api, errorMessage } from './api';

describe('API cookie/CSRF contract', () => {
  let api: Api;
  let http: HttpTestingController;
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    api = TestBed.inject(Api);
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());
  it('gets a fresh identity-bound CSRF token for every mutation, with no browser token storage', async () => {
    for (const method of ['POST', 'PUT', 'DELETE'] as const) {
      const pending = api.mutate(method, '/test', method === 'PUT' ? { isEnabled: false } : null);
      http.expectOne('/api/auth/csrf').flush({ token: method });
      const request = await vi.waitFor(() => http.expectOne('/api/test'));
      expect(request.request.method).toBe(method);
      expect(request.request.headers.get('X-CSRF-TOKEN')).toBe(method);
      if (method === 'PUT') expect(request.request.body).toEqual({ isEnabled: false });
      request.flush(null);
      await pending;
    }
  });
  it('does not mutate if token acquisition fails', async () => {
    const pending = api.mutate('DELETE', '/test');
    http.expectOne('/api/auth/csrf').flush(null, { status: 400, statusText: 'Bad request' });
    await expect(pending).rejects.toBeInstanceOf(HttpErrorResponse);
    http.expectNone('/api/test');
  });
  it('clears an expired current session', async () => {
    api.session.set({ id: 'u', email: 'user', isAdmin: false });
    const pending = api.current();
    http.expectOne('/api/auth/me').flush(null, { status: 401, statusText: 'Unauthorized' });
    expect(await pending).toBeNull();
    expect(api.session()).toBeNull();
  });
  it('clears navigation state when a protected request returns 401', async () => {
    api.session.set({ id: 'u', email: 'user', isAdmin: true });
    const pending = api.get('/admin/users');
    http.expectOne('/api/admin/users').flush(null, { status: 401, statusText: 'Unauthorized' });
    await expect(pending).rejects.toBeInstanceOf(HttpErrorResponse);
    expect(api.session()).toBeNull();
  });
  it.each([0, 400, 401, 403, 404, 409, 500])('provides safe feedback for HTTP %s', (status) => {
    const message = errorMessage(new HttpErrorResponse({ status, error: 'secret server detail' }));
    expect(message.length).toBeGreaterThan(15);
    expect(message).not.toContain('secret server detail');
  });
});
