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
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { daysInMonth, formatClosingDate, toIsoDate } from '../../closing-date-helpers';
import { ClosingScheduleRow } from '../../types/closing-schedule-row';
import { ClosingMonthChange } from '../../types/closing-month-change';
import { ClosingMonthRef } from '../../types/closing-month-ref';

@Component({
  selector: 'app-card-closing-schedule',
  host: { class: 'block' },
  imports: [ReactiveFormsModule],
  templateUrl: './card-closing-schedule.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CardClosingSchedule {
  cardId: InputSignal<string> = input.required<string>();
  cardName: InputSignal<string> = input.required<string>();
  typeLabel: InputSignal<string> = input<string>('');
  nextClosingText: InputSignal<string> = input<string>('—');
  schedule: InputSignal<ClosingScheduleRow[]> = input.required<ClosingScheduleRow[]>();
  busy: InputSignal<boolean> = input<boolean>(false);
  errorText: InputSignal<string | null> = input<string | null>(null);
  closingSet: OutputEmitterRef<ClosingMonthChange> = output<ClosingMonthChange>();
  closingReset: OutputEmitterRef<ClosingMonthRef> = output<ClosingMonthRef>();

  protected readonly editingMonth: WritableSignal<string | null> = signal<string | null>(null);
  protected readonly editingInHeader: WritableSignal<boolean> = signal(false);
  protected readonly dateControl: FormControl<string> = new FormControl<string>('', {
    nonNullable: true,
    validators: Validators.required,
  });

  protected formatDate(row: ClosingScheduleRow): string {
    return formatClosingDate(row.closingDate);
  }

  protected monthKey(row: ClosingScheduleRow): string {
    return `${row.year}-${row.month}`;
  }

  protected minDate(row: ClosingScheduleRow): string {
    return toIsoDate(row.year, row.month, 1);
  }

  protected maxDate(row: ClosingScheduleRow): string {
    return toIsoDate(row.year, row.month, daysInMonth(row.year, row.month));
  }

  protected startMonthEdit(row: ClosingScheduleRow, inHeader: boolean = false): void {
    this.dateControl.setValue(row.closingDate);
    this.editingInHeader.set(inHeader);
    this.editingMonth.set(this.monthKey(row));
  }

  protected cancelMonthEdit(): void {
    this.editingInHeader.set(false);
    this.editingMonth.set(null);
  }

  protected saveMonth(row: ClosingScheduleRow): void {
    const value: string = this.dateControl.value;
    if(value < this.minDate(row) || value > this.maxDate(row)) {
      this.dateControl.markAsTouched();
      return;
    }
    this.editingInHeader.set(false);
    this.editingMonth.set(null);
    this.closingSet.emit({ year: row.year, month: row.month, day: Number(value.slice(8, 10)) });
  }

  protected resetMonth(row: ClosingScheduleRow): void {
    this.closingReset.emit({ year: row.year, month: row.month });
  }
}
