import { Routes } from '@angular/router';

import { MoneyFlowPage } from './pages/money-flow-page/money-flow-page';
import { RecordIncomePage } from './pages/record-income-page/record-income-page';
import { ReverseMovementPage } from './pages/reverse-movement-page/reverse-movement-page';
import { TransactionsPage } from './pages/transactions-page/transactions-page';

export const routes: Routes = [
  { path: 'transactions', component: TransactionsPage },
  { path: 'transactions/:id/reverse', component: ReverseMovementPage },
  { path: 'incomes/new', component: RecordIncomePage },
  { path: 'money-flow', component: MoneyFlowPage }
];
