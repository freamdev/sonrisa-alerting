import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { Api } from './api';
export const signedIn: CanActivateFn = async () => {
  const api = inject(Api);
  const router = inject(Router);
  try {
    return (await api.current()) ? true : router.parseUrl('/login');
  } catch {
    return router.parseUrl('/login');
  }
};
export const adminOnly: CanActivateFn = async () => {
  const api = inject(Api);
  const router = inject(Router);
  try {
    const session = await api.current();
    return session?.isAdmin ? true : router.parseUrl(session ? '/personal' : '/login');
  } catch {
    return router.parseUrl('/login');
  }
};
