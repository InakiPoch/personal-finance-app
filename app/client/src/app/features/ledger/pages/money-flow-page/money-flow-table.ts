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
import { MoneyFlowRow } from '../../types/money-flow-row';

export type MoneyFlowCurrencyTotal = { currencyCode: CurrencyCode; income: Money; outcome: Money };

@Component({
  selector: 'app-money-flow-table',
  imports: [],
  templateUrl: './money-flow-table.html',
  styleUrl: './money-flow-table.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class MoneyFlowTable {
  readonly rows: InputSignal<MoneyFlowRow[]> = input.required<MoneyFlowRow[]>();
  readonly footerTotals: InputSignal<MoneyFlowCurrencyTotal[]> = input.required<MoneyFlowCurrencyTotal[]>();
  readonly undoing: InputSignal<boolean> = input<boolean>(false);
  readonly undo: OutputEmitterRef<string> = output<string>();

  protected readonly formatMoney: (value: Money, code: CurrencyCode) => string = formatMoney;

  protected datePart(row: MoneyFlowRow): string {
    return row.date.slice(0, 10);
  }

  protected flagLabel(row: MoneyFlowRow): string | null {
    if(row.flag === null) {
      return null;
    }
    const labels: Record<NonNullable<MoneyFlowRow['flag']>, string> = {
      LentTo: 'Lent to',
      SharedWith: 'Shared with',
      PaidBackBy: 'Paid back by'
    };
    return `${labels[row.flag]} ${row.partyName ?? ''}`.trim();
  }

  protected isUndoable(row: MoneyFlowRow): boolean {
    return row.kind === 'Income' || row.flag === 'LentTo';
  }

  protected onUndo(row: MoneyFlowRow): void {
    if(this.isUndoable(row)) {
      this.undo.emit(row.transactionId);
    }
  }
}
