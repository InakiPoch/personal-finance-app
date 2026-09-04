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
import { MonthlyStatementInstallment } from '../../types/monthly-statement-installment';

@Component({
  selector: 'app-installments-table',
  imports: [],
  templateUrl: './installments-table.html',
  styleUrl: './installments-table.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class InstallmentsTable {
  readonly installments: InputSignal<MonthlyStatementInstallment[]> =
    input.required<MonthlyStatementInstallment[]>();
  readonly reverseClick: OutputEmitterRef<string> = output<string>();

  protected readonly formatArs: (value: Money) => string = formatArs;

  protected canReverse(installment: MonthlyStatementInstallment): boolean {
    return installment.reversalTransactionId !== null && !installment.isReversed;
  }

  protected onReverse(installment: MonthlyStatementInstallment): void {
    if(installment.reversalTransactionId !== null) {
      this.reverseClick.emit(installment.reversalTransactionId);
    }
  }
}
