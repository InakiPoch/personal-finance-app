import { Routes } from '@angular/router';

/**
 * Lazy routing shell. Each feature owns a `*.routes.ts`.
 * 
 * Wired so far: `reports` (the dashboard, and the default route), `instruments`.
 * The remaining Phase 1 steps append `financing` and `ledger` entries; a
 * `{ path: '**', redirectTo: 'reports' }` wildcard lands with the last feature step.
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
];
