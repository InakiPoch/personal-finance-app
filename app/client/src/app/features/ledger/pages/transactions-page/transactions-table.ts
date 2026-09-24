import {
  ChangeDetectionStrategy,
  Component,
  InputSignal,
  OutputEmitterRef,
  input,
  output,
} from '@angular/core';
import { formatMoney } from '../../../../core/money/money';
import { CurrencyCode } from '../../../../core/types/currency-code';
import { Money } from '../../../../core/types/money';
import { TransactionRow } from '../../types/transaction-row';

@Component({
  selector: 'app-transactions-table',
  imports: [],
  templateUrl: './transactions-table.html',
  styleUrl: './transactions-table.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class TransactionsTable {
  readonly transactions: InputSignal<TransactionRow[]> = input.required<TransactionRow[]>();
  readonly reverseTransaction: OutputEmitterRef<string> = output<string>();

  protected readonly formatMoney: (value: Money, code: CurrencyCode) => string = formatMoney;

  protected datePart(row: TransactionRow): string {
    return row.postedOnUtc.slice(0, 10);
  }

  protected isLocked(row: TransactionRow): boolean {
    return row.isReversal || row.isReversed;
  }

  protected lockReason(row: TransactionRow): string {
    return row.isReversal ? 'Reversal entry' : 'Already reversed';
  }

  protected onReverse(transactionId: string): void {
    this.reverseTransaction.emit(transactionId);
  }
}
