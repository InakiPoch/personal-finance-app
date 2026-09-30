import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';

type NavItem = { label: string; path: string };
type NavGroup = { label: string; kind: 'view' | 'setup' | 'action'; items: NavItem[] };

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './app.html',
  styleUrl: './app.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class App {
  protected readonly navGroups: NavGroup[] = [
    {
      label: 'Views',
      kind: 'view',
      items: [
        { label: 'Dashboard', path: '/reports' },
        { label: 'Recent Money Movements', path: '/ledger/money-flow' },
        { label: 'Owed to Creditors', path: '/financing/creditor-payables' },
        { label: 'Parties', path: '/parties' },
        { label: 'Subscriptions', path: '/subscriptions' },
        { label: 'Credit Card Cycles', path: '/financing/statements' }
      ]
    },
    {
      label: 'Setup',
      kind: 'setup',
      items: [
        { label: 'Cards and Accounts', path: '/instruments' },
        { label: 'Creditors', path: '/creditors' }
      ]
    },
    {
      label: 'Actions',
      kind: 'action',
      items: [
        { label: 'Load an Expense', path: '/financing/load-expense' },
        { label: 'Reverse a Transaction', path: '/ledger/transactions' }
      ]
    }
  ];
}
