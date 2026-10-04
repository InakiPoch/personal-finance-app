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
import { MonthlyStatementSummary } from '../../types/monthly-statement-summary';

@Component({
  selector: 'app-statements-table',
  imports: [],
  templateUrl: './statements-table.html',
  styleUrl: './statements-table.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class StatementsTable {
  readonly statements: InputSignal<MonthlyStatementSummary[]> = input.required<MonthlyStatementSummary[]>();
  readonly openStatement: OutputEmitterRef<string> = output<string>();

  protected readonly formatMoney: (value: Money, code: CurrencyCode) => string = formatMoney;

  protected cycleLabel(statement: MonthlyStatementSummary): string {
    return `${statement.cycleYear}-${String(statement.cycleMonth).padStart(2, '0')}`;
  }

  protected onOpen(statementId: string): void {
    this.openStatement.emit(statementId);
  }
}
