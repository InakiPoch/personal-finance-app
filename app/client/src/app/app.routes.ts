import { Routes } from '@angular/router';

/**
 * Lazy routing shell. Each feature owns a `*.routes.ts`.
 *
 * Wired: `reports` (the dashboard, and the default route), `instruments`, `financing`,
 * `ledger`. Unknown paths fall back to the dashboard.
 */
export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'reports' },
  {
    path: 'reports',
    loadChildren: () => import('./features/reports/reports.routes').then((m) => m.routes),
  },
  {
    path: 'instruments',
    loadChildren: () => import('./features/instruments/instruments.routes').then((m) => m.routes),
  },
  {
    path: 'financing',
    loadChildren: () => import('./features/financing/financing.routes').then((m) => m.routes),
  },
  {
    path: 'ledger',
    loadChildren: () => import('./features/ledger/ledger.routes').then((m) => m.routes),
  },
  { path: '**', redirectTo: 'reports' },
];
