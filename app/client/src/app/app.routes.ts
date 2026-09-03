import { Routes } from '@angular/router';

/**
 * Lazy routing shell. Each feature owns a `*.routes.ts`.
 * 
 * Wired so far: `reports` (the dashboard, and the default route), `instruments`,
 * `financing`. Step 1.7 appends `ledger` and the `{ path: '**', redirectTo: 'reports' }`
 * wildcard.
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
];
