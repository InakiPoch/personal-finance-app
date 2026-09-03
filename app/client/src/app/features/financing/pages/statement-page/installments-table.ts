import { ChangeDetectionStrategy, Component, InputSignal, input } from '@angular/core';
import { formatArs } from '../../../../core/money/money';
import { Money } from '../../../../core/types/money';
import { MonthlyStatementInstallment } from '../../types/monthly-statement-installment';

@Component({
  selector: 'app-installments-table',
  imports: [],
  templateUrl: './installments-table.html',
  styleUrl: './installments-table.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class InstallmentsTable {
  readonly installments: InputSignal<MonthlyStatementInstallment[]> =
    input.required<MonthlyStatementInstallment[]>();

  protected readonly formatArs: (value: Money) => string = formatArs;
}
