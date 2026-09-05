import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';

type NavItem = { label: string; path: string };

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './app.html',
  styleUrl: './app.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class App {
  protected readonly navItems: NavItem[] = [
    { label: 'Dashboard', path: '/reports' },
    { label: 'Instruments', path: '/instruments' },
    { label: 'Load expense', path: '/financing/load-expense' },
    { label: 'Statements', path: '/financing/statements' },
    { label: 'Recent purchases', path: '/financing/recent-purchases' },
    { label: 'Reverse', path: '/ledger/transactions' },
    { label: 'Subscriptions', path: '/subscriptions' },
    { label: 'Parties', path: '/parties' },
    { label: 'Creditors', path: '/creditors' }
  ];
}
