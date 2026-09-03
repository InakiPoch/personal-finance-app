import { ChangeDetectionStrategy, Component, InputSignal, input } from '@angular/core';
import { formatArs } from '../../../../core/money/money';
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

  protected readonly formatArs: (value: Money) => string = formatArs;

  protected signed(value: Money): string {
    const formatted: string = this.formatArs(value);
    return value > 0 ? `+${formatted}` : formatted;
  }
}
