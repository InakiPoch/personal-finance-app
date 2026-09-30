import {
  ChangeDetectionStrategy,
  Component,
  InputSignal,
  OutputEmitterRef,
  WritableSignal,
  input,
  output,
  signal,
} from '@angular/core';
import { formatMoney } from '../../../../core/money/money';
import { CurrencyCode } from '../../../../core/types/currency-code';
import { Money } from '../../../../core/types/money';
import { TransactionFeedRow } from '../../../reports/types/transaction-feed-row';

@Component({
  selector: 'app-transactions-table',
  imports: [],
  templateUrl: './transactions-table.html',
  styleUrl: './transactions-table.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class TransactionsTable {
  readonly transactions: InputSignal<TransactionFeedRow[]> = input.required<TransactionFeedRow[]>();
  readonly reverseTransaction: OutputEmitterRef<string> = output<string>();

  protected readonly expandedId: WritableSignal<string | null> = signal<string | null>(null);
  protected readonly formatMoney: (value: Money, code: CurrencyCode) => string = formatMoney;

  protected datePart(row: TransactionFeedRow): string {
    return row.postedOnUtc.slice(0, 10);
  }

  protected isLocked(row: TransactionFeedRow): boolean {
    return row.isUndoEntry || row.isUndone;
  }

  protected lockReason(row: TransactionFeedRow): string {
    return row.isUndoEntry ? 'Undo entry' : 'Already undone';
  }

  protected route(row: TransactionFeedRow): string {
    return `${row.fromAccounts.join(', ')} → ${row.toAccounts.join(', ')}`;
  }

  protected canExpand(row: TransactionFeedRow): boolean {
    return !row.isUndoEntry && row.impactLines.length > 0;
  }

  protected toggle(id: string): void {
    this.expandedId.update((current: string | null) => (current === id ? null : id));
  }

  protected onReverse(transactionId: string): void {
    this.reverseTransaction.emit(transactionId);
  }
}
