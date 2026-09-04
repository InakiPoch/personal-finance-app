import { Routes } from '@angular/router';

import { ReverseMovementPage } from './pages/reverse-movement-page/reverse-movement-page';
import { TransactionsPage } from './pages/transactions-page/transactions-page';

export const routes: Routes = [
  { path: 'transactions', component: TransactionsPage },
  { path: 'transactions/:id/reverse', component: ReverseMovementPage }
];
