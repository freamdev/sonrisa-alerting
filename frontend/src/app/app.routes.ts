import { Routes } from '@angular/router';
import { Login } from './login';
import { Personal } from './personal';
import { Admin } from './admin';
import { signedIn, adminOnly } from './guards';

export const routes: Routes = [
  { path: 'login', component: Login },
  { path: 'personal', component: Personal, canActivate: [signedIn] },
  { path: 'admin', component: Admin, canActivate: [adminOnly] },
  { path: '**', redirectTo: 'personal' },
];
