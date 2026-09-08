import { ChangeDetectionStrategy, Component, InputSignal, input } from '@angular/core';
import { formatArs } from '../../../../core/money/money';
import { Money } from '../../../../core/types/money';
import { RecentPurchaseRow } from '../../types/recent-purchase-row';

const MONTH_LABELS: readonly string[] = [
  'Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'
];

@Component({
  selector: 'app-recent-purchases-table',
  imports: [],
  templateUrl: './recent-purchases-table.html',
  styleUrl: './recent-purchases-table.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class RecentPurchasesTable {
  readonly purchases: InputSignal<RecentPurchaseRow[]> = input.required<RecentPurchaseRow[]>();

  protected readonly formatArs: (value: Money) => string = formatArs;

  protected paidLabel(purchase: RecentPurchaseRow): string {
    return `${purchase.paidInstallmentCount}/${purchase.installmentCount} paid`;
  }

  protected hasPending(purchase: RecentPurchaseRow): boolean {
    return purchase.pendingAmountMinorUnits > 0;
  }

  protected pendingLabel(purchase: RecentPurchaseRow): string {
    return `${formatArs(purchase.pendingAmountMinorUnits)} pending`;
  }

  protected nextPaymentLabel(purchase: RecentPurchaseRow): string {
    if(purchase.nextDueYear === null || purchase.nextDueMonth === null) {
      return 'Fully paid';
    }
    return `next: ${MONTH_LABELS[purchase.nextDueMonth - 1]} ${purchase.nextDueYear}`;
  }
}
