import { Routes } from '@angular/router';
import { CreditorPayablesPage } from './pages/creditor-payables-page/creditor-payables-page';
import { LoadExpensePage } from './pages/load-expense-page/load-expense-page';
import { RecentPurchasesPage } from './pages/recent-purchases-page/recent-purchases-page';
import { StatementPage } from './pages/statement-page/statement-page';
import { StatementsPage } from './pages/statements-page/statements-page';

export const routes: Routes = [
  { path: 'load-expense', component: LoadExpensePage },
  { path: 'statements', component: StatementsPage },
  { path: 'statements/:id', component: StatementPage },
  { path: 'recent-purchases', component: RecentPurchasesPage },
  { path: 'creditor-payables', component: CreditorPayablesPage }
];
