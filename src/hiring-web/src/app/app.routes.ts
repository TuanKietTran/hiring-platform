import type { Routes } from '@angular/router';
import { authGuard } from './core/guards';
import { ApplicantProfilePage } from './pages/applicant-profile';
import { LoginPage, RegisterPage } from './pages/auth';
import { Dashboard } from './pages/dashboard';
import { Home } from './pages/home';
import { JobDetail } from './pages/job-detail';
import { Jobs } from './pages/jobs';

export const routes: Routes = [
  { path: '', component: Home },
  { path: 'jobs', component: Jobs },
  { path: 'jobs/:id', component: JobDetail },
  { path: 'login', component: LoginPage },
  { path: 'register', component: RegisterPage },
  { path: 'dashboard', component: Dashboard, canActivate: [authGuard] },
  { path: 'profile', component: ApplicantProfilePage, canActivate: [authGuard] },
  { path: '**', redirectTo: '' },
];
