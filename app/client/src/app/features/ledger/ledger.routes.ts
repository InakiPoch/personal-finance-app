import { Routes } from '@angular/router';

import { ReverseIndex } from './pages/reverse-index/reverse-index';
import { ReverseMovementPage } from './pages/reverse-movement-page/reverse-movement-page';

export const routes: Routes = [
  { path: 'transactions', component: ReverseIndex },
  { path: 'transactions/:id/reverse', component: ReverseMovementPage },
];
