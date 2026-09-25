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
import { PartyTimelineRow } from '../../../reports/types/party-timeline-row';

@Component({
  selector: 'app-timeline-table',
  imports: [],
  templateUrl: './timeline-table.html',
  styleUrl: './timeline-table.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class TimelineTable {
  readonly rows: InputSignal<PartyTimelineRow[]> = input.required<PartyTimelineRow[]>();
  readonly reverseClick: OutputEmitterRef<string> = output<string>();

  protected readonly formatMoney: (value: Money, code: CurrencyCode) => string = formatMoney;

  protected signed(row: PartyTimelineRow): string {
    const formatted: string = this.formatMoney(row.deltaMinorUnits, row.currencyCode);
    return row.deltaMinorUnits > 0 ? `+${formatted}` : formatted;
  }

  protected isReversalRow(row: PartyTimelineRow): boolean {
    return row.description === 'Reversal';
  }

  protected onReverse(row: PartyTimelineRow): void {
    this.reverseClick.emit(row.transactionId);
  }
}
