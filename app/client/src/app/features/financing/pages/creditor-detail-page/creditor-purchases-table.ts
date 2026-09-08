import { ChangeDetectionStrategy, Component, InputSignal, input } from '@angular/core';
import { formatArs } from '../../../../core/money/money';
import { Money } from '../../../../core/types/money';
import { CreditorInstallmentRow } from '../../types/creditor-installment-row';
import { CreditorPurchaseGroup } from '../../types/creditor-purchase-group';

const MONTH_LABELS: readonly string[] = [
  'Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'
];

@Component({
  selector: 'app-creditor-purchases-table',
  imports: [],
  templateUrl: './creditor-purchases-table.html',
  styleUrl: './creditor-purchases-table.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class CreditorPurchasesTable {
  readonly purchases: InputSignal<CreditorPurchaseGroup[]> =
    input.required<CreditorPurchaseGroup[]>();

  protected readonly formatArs: (value: Money) => string = formatArs;

  protected installmentLabel(row: CreditorInstallmentRow): string {
    return `${row.sequence}/${row.installmentCount}`;
  }

  protected dueLabel(row: CreditorInstallmentRow): string {
    return `${MONTH_LABELS[row.dueMonth - 1]} ${row.dueYear}`;
  }
}
