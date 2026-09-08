import {
  ChangeDetectionStrategy,
  Component,
  InputSignal,
  OutputEmitterRef,
  input,
  output,
} from '@angular/core';
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
  readonly purchases: InputSignal<CreditorPurchaseGroup[]> = input.required<CreditorPurchaseGroup[]>();
  readonly paying: InputSignal<boolean> = input<boolean>(false);
  readonly payClick: OutputEmitterRef<string> = output<string>();
  readonly undoClick: OutputEmitterRef<string> = output<string>();

  protected readonly formatArs: (value: Money) => string = formatArs;

  protected installmentLabel(row: CreditorInstallmentRow): string {
    return `${row.sequence}/${row.installmentCount}`;
  }

  protected dueLabel(row: CreditorInstallmentRow): string {
    return `${MONTH_LABELS[row.dueMonth - 1]} ${row.dueYear}`;
  }

  protected canPay(row: CreditorInstallmentRow): boolean {
    return !row.isPaid && !row.isReversed;
  }

  protected onPay(row: CreditorInstallmentRow): void {
    if(this.canPay(row)) {
      this.payClick.emit(row.installmentId);
    }
  }

  protected onUndo(row: CreditorInstallmentRow): void {
    if(row.isPaid) {
      this.undoClick.emit(row.installmentId);
    }
  }
}
