import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import {
  ActivatedRouteSnapshot,
  provideRouter,
  Router,
  RouterStateSnapshot,
  UrlTree,
} from '@angular/router';
import { Api, Session } from './api';
import { App } from './app';
import { adminOnly, signedIn } from './guards';

describe('Role-limited navigation', () => {
  function setup(session: Session | null) {
    const api = {
      session: signal(session),
      current: vi.fn().mockResolvedValue(session),
      mutate: vi.fn().mockResolvedValue(undefined),
    };
    TestBed.configureTestingModule({
      providers: [provideRouter([]), { provide: Api, useValue: api }],
    });
    return api;
  }
  const user = { id: 'u', email: 'user@example.test', isAdmin: false };
  it.each([false, true])(
    'shows personal navigation for user/admin and admin link only for admin=%s',
    (isAdmin) => {
      setup({ ...user, isAdmin });
      const fixture = TestBed.createComponent(App);
      fixture.detectChanges();
      const links = Array.from(
        fixture.nativeElement.querySelectorAll('a') as NodeListOf<HTMLAnchorElement>,
      ).map((x) => x.getAttribute('href'));
      expect(links).toContain('/personal');
      expect(links.includes('/admin')).toBe(isAdmin);
    },
  );
  it.each([
    [null, '/login'],
    [user, '/personal'],
    [{ ...user, isAdmin: true }, true],
  ])('guards admin route based on a fresh server session', async (session, expected) => {
    const api = setup(session as Session | null);
    const result = await TestBed.runInInjectionContext(() =>
      adminOnly({} as ActivatedRouteSnapshot, {} as RouterStateSnapshot),
    );
    expect(result instanceof UrlTree ? TestBed.inject(Router).serializeUrl(result) : result).toBe(
      expected,
    );
    expect(api.current).toHaveBeenCalled();
  });
  it('redirects anonymous personal access to sign in', async () => {
    setup(null);
    const result = await TestBed.runInInjectionContext(() =>
      signedIn({} as ActivatedRouteSnapshot, {} as RouterStateSnapshot),
    );
    expect(TestBed.inject(Router).serializeUrl(result as UrlTree)).toBe('/login');
  });
});
