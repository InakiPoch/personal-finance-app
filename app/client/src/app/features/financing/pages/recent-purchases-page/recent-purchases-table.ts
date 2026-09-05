import { ChangeDetectionStrategy, Component, InputSignal, input } from '@angular/core';
import { formatArs } from '../../../../core/money/money';
import { Money } from '../../../../core/types/money';
import { RecentPurchaseRow } from '../../types/recent-purchase-row';

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

  protected installmentLabel(purchase: RecentPurchaseRow): string {
    return purchase.installmentCount === 1 ? '1 installment' : `${purchase.installmentCount} installments`;
  }
}
