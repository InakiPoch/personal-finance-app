import { Routes } from '@angular/router';
import { LoadExpensePage } from './pages/load-expense-page/load-expense-page';
import { StatementIndex } from './pages/statement-index/statement-index';
import { StatementPage } from './pages/statement-page/statement-page';

export const routes: Routes = [
  { path: 'load-expense', component: LoadExpensePage },
  { path: 'statements', component: StatementIndex },
  { path: 'statements/:id', component: StatementPage },
];
