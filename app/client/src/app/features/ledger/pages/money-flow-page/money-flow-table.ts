import { ChangeDetectionStrategy, Component, InputSignal, input } from '@angular/core';
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

  protected readonly formatMoney: (value: Money, code: CurrencyCode) => string = formatMoney;

  protected datePart(row: MoneyFlowRow): string {
    return row.date.slice(0, 10);
  }
}
