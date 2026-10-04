import {
  ChangeDetectionStrategy,
  Component,
  OnDestroy,
  OnInit,
  WritableSignal,
  inject,
  signal,
} from '@angular/core';
import { FormBuilder, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Observable, Subject, forkJoin, takeUntil } from 'rxjs';
import { AppError } from '../../../../core/types/app-error';
import { InstrumentType } from '../../../../core/types/instrument-type';
import { CardClosingSchedule } from '../../components/card-closing-schedule/card-closing-schedule';
import { formatClosingDate } from '../../closing-date-helpers';
import { InstrumentsService } from '../../instruments-service';
import { ClosingMonthChange } from '../../types/closing-month-change';
import { ClosingMonthRef } from '../../types/closing-month-ref';
import { ClosingScheduleRow } from '../../types/closing-schedule-row';
import { CreateInstrument } from '../../types/create-instrument';
import { Instrument } from '../../types/instrument';
import { creditRequiresCutoff } from '../../validation-helpers';

type SubmitStatus = 'idle' | 'submitting' | 'error';

type ListStatus = 'loading' | 'error' | 'ready';

type TypeOption = { value: InstrumentType; label: string };

type InstrumentForm = FormGroup<{
  type: FormControl<InstrumentType>;
  name: FormControl<string>;
  cutoffDate: FormControl<number | null>;
}>;

@Component({
  selector: 'app-instruments-page',
  imports: [ReactiveFormsModule, CardClosingSchedule],
  templateUrl: './instruments-page.html',
  styleUrl: './instruments-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class InstrumentsPage implements OnInit, OnDestroy {
  protected form!: InstrumentForm;
  protected readonly instruments: WritableSignal<Instrument[]> = signal<Instrument[]>([]);
  protected readonly submitStatus: WritableSignal<SubmitStatus> = signal<SubmitStatus>('idle');
  protected readonly submitError: WritableSignal<AppError | null> = signal<AppError | null>(null);
  protected readonly nameErrors: Record<string, string> = { required: 'Name is required.' };
  protected readonly cutoffErrors: Record<string, string> = { creditCutoff: 'Credit cards need a usual closing day between 1 and 31.' };
  protected readonly schedules: WritableSignal<Record<string, ClosingScheduleRow[] | undefined>> =
    signal<Record<string, ClosingScheduleRow[] | undefined>>({});
  protected readonly closingBusy: WritableSignal<Record<string, boolean | undefined>> =
    signal<Record<string, boolean | undefined>>({});
  protected readonly closingErrors: WritableSignal<Record<string, string | null | undefined>> =
    signal<Record<string, string | null | undefined>>({});
  protected readonly listStatus: WritableSignal<ListStatus> = signal<ListStatus>('loading');
  protected readonly typeOptions: readonly TypeOption[] = [
    { value: 'debit', label: 'Debit account' },
    { value: 'credit', label: 'Credit card' },
    { value: 'cash', label: 'Cash' },
  ];

  private readonly fb: FormBuilder = inject(FormBuilder);
  private readonly instrumentsService: InstrumentsService = inject(InstrumentsService);
  private readonly submitErrorMessages: Record<string, string> = {
    'Instruments.UnknownType': 'That instrument type is not supported.',
    'Http.BadRequest': 'The instrument could not be created — check the values and try again.',
    'Http.Conflict': 'That instrument already exists.',
    'Http.ServerError': 'Something went wrong on the server. Try again in a moment.',
    'Http.NetworkError': 'Could not reach the server. Check your connection.'
  };
  private readonly closingErrorMessages: Record<string, string> = {
    'Financing.ClosingChangeMovesChargedPurchase': 'Some purchases in that month are already on a card bill, so the closing date can\'t move them. Set this month\'s closing date only.',
    'Financing.ClosingMonthLocked': 'That month\'s card bill is already out; its closing date can\'t change.',
    'Financing.InvalidClosingDay': 'Pick a valid closing day for that month.'
  };
  private readonly destroy$: Subject<void> = new Subject<void>();

  protected onSubmit(): void {
    if(this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const raw: { type: InstrumentType; name: string; cutoffDate: number | null } =
      this.form.getRawValue();
    const body: CreateInstrument = {
      type: raw.type,
      name: raw.name.trim(),
      ...(raw.type === 'credit' && raw.cutoffDate !== null ? { cutoffDate: raw.cutoffDate } : {})
    };
    this.submitError.set(null);
    this.submitStatus.set('submitting');
    this.instrumentsService
      .create(body)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.loadInstruments();
          this.form.reset({ type: 'debit', name: '', cutoffDate: null });
          this.submitStatus.set('idle');
        },
        error: (error: AppError) => {
          this.submitError.set(error);
          this.submitStatus.set('error');
        }
      }
    );
  }

  protected submitErrorText(error: AppError): string {
    return this.submitErrorMessages[error.code] ?? 'The instrument could not be created.';
  }

  protected typeLabel(type: InstrumentType): string {
    return this.typeOptions.find((option: TypeOption): boolean => option.value === type)?.label ?? type;
  }

  protected nextClosingText(instrument: Instrument): string {
    return instrument.nextClosingDate === null ? '—' : formatClosingDate(instrument.nextClosingDate);
  }

  protected onClosingSet(card: Instrument, change: ClosingMonthChange): void {
    this.runClosingEdit(
      card.id,
      this.instrumentsService.setClosingDate(card.id, change.year, change.month, change.day),
    );
  }

  protected onClosingReset(card: Instrument, month: ClosingMonthRef): void {
    this.runClosingEdit(
      card.id,
      this.instrumentsService.clearClosingDate(card.id, month.year, month.month),
    );
  }

  private runClosingEdit(cardId: string, call: Observable<void>): void {
    this.closingBusy.update((busy: Record<string, boolean | undefined>) => ({ ...busy, [cardId]: true }));
    this.closingErrors.update((errors: Record<string, string | null | undefined>) => ({ ...errors, [cardId]: null }));
    call.pipe(takeUntil(this.destroy$)).subscribe({
      next: () => {
        this.closingBusy.update((busy: Record<string, boolean | undefined>) => ({ ...busy, [cardId]: false }));
        this.reloadCard(cardId);
      },
      error: (error: AppError) => {
        this.closingBusy.update((busy: Record<string, boolean | undefined>) => ({ ...busy, [cardId]: false }));
        this.closingErrors.update((errors: Record<string, string | null | undefined>) => ({
          ...errors,
          [cardId]: this.closingErrorMessages[error.code] ?? 'The closing date could not be saved.',
        }));
      },
    });
  }

  private reloadCard(cardId: string): void {
    this.instrumentsService
      .list()
      .pipe(takeUntil(this.destroy$))
      .subscribe({ next: (rows: Instrument[]) => this.instruments.set(rows) });
    this.loadSchedules([cardId]);
  }

  private loadSchedules(cardIds: string[]): void {
    if(cardIds.length === 0) {
      return;
    }
    forkJoin(cardIds.map((id: string) => this.instrumentsService.closingSchedule(id)))
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (all: ClosingScheduleRow[][]) =>
          this.schedules.update((current: Record<string, ClosingScheduleRow[] | undefined>) => {
            const next: Record<string, ClosingScheduleRow[] | undefined> = { ...current };
            cardIds.forEach((id: string, index: number) => (next[id] = all[index]));
            return next;
          }),
        // Schedules are secondary; the register still renders without them.
        error: () => undefined,
      });
  }

  private loadInstruments(): void {
    this.listStatus.set('loading');
    this.instrumentsService
      .list()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (rows: Instrument[]) => {
          this.instruments.set(rows);
          this.listStatus.set('ready');
          this.loadSchedules(
            rows.filter((row: Instrument) => row.type === 'credit').map((row: Instrument) => row.id),
          );
        },
        error: () => this.listStatus.set('error'),
      });
  }

  private initInstrumentForm(): void {
    this.form = this.fb.group(
      {
        type: this.fb.nonNullable.control<InstrumentType>('debit', {
          validators: Validators.required,
        }),
        name: this.fb.nonNullable.control('', { validators: Validators.required }),
        cutoffDate: this.fb.control<number | null>(null),
      },
      { validators: creditRequiresCutoff },
    );
  }

  ngOnInit(): void {
    this.initInstrumentForm();
    this.loadInstruments();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }
}
