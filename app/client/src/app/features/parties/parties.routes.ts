import { Routes } from '@angular/router';

import { PartiesPage } from './pages/parties-page/parties-page';
import { PartyDetailPage } from './pages/party-detail-page/party-detail-page';
import { SharedExpensePage } from './pages/shared-expense-page/shared-expense-page';

export const routes: Routes = [
  { path: '', component: PartiesPage },
  { path: 'shared-expense', component: SharedExpensePage },
  { path: ':id', component: PartyDetailPage }
];
