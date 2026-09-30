import { ChangeDetectionStrategy, Component, InputSignal, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { formatMoney } from '../../../../core/money/money';
import { CurrencyCode } from '../../../../core/types/currency-code';
import { Money } from '../../../../core/types/money';
import { CreditorPayableRow } from '../../types/creditor-payable-row';

@Component({
  selector: 'app-creditor-payables-table',
  imports: [RouterLink],
  templateUrl: './creditor-payables-table.html',
  styleUrl: './creditor-payables-table.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class CreditorPayablesTable {
  readonly payables: InputSignal<CreditorPayableRow[]> = input.required<CreditorPayableRow[]>();

  protected readonly formatMoney: (value: Money, code: CurrencyCode) => string = formatMoney;

  protected accountsLabel(row: CreditorPayableRow): string {
    return row.accounts.map((a) => a.label).join(', ');
  }
}
