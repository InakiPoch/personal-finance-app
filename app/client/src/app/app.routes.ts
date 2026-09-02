import { Routes } from '@angular/router';

/**
 * Routing shell — intentionally empty in Phase 0.
 *
 * Each feature owns a `*.routes.ts` and is lazy-wired here in its Phase 1 step
 * (see docs/DESIGN.md §10, .claude/TASK.md). Fase-1 features first; Fase-2/3
 * (`subscriptions`, `parties`) are added in their own phases.
 *
 * Intended shape once the features exist:
 *
 *   { path: '', pathMatch: 'full', redirectTo: 'reports' },            // dashboard is the default
 *   { path: 'reports',     loadChildren: () => import('./features/reports/reports.routes').then(m => m.routes) },
 *   { path: 'instruments', loadChildren: () => import('./features/instruments/instruments.routes').then(m => m.routes) },
 *   { path: 'financing',   loadChildren: () => import('./features/financing/financing.routes').then(m => m.routes) },
 *   { path: 'ledger',      loadChildren: () => import('./features/ledger/ledger.routes').then(m => m.routes) },
 *
 * A wildcard (`{ path: '**', redirectTo: 'reports' }`) lands with the last feature step.
 */
export const routes: Routes = [];
