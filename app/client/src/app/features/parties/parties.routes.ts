import { Routes } from '@angular/router';

import { PartiesPage } from './pages/parties-page/parties-page';
import { PartyDetailPage } from './pages/party-detail-page/party-detail-page';

export const routes: Routes = [
  { path: '', component: PartiesPage },
  { path: ':id', component: PartyDetailPage }
];
