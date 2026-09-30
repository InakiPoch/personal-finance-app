import {
  ChangeDetectionStrategy,
  Component,
  OnDestroy,
  OnInit,
  Signal,
  WritableSignal,
  computed,
  inject,
  signal,
} from '@angular/core';
import { FormBuilder, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { Subject, takeUntil } from 'rxjs';
import { toMinorUnits } from '../../../../core/money/money';
import { AppError } from '../../../../core/types/app-error';
import { CurrencyCode } from '../../../../core/types/currency-code';
import { Money } from '../../../../core/types/money';
import { InstrumentsService } from '../../../instruments/instruments-service';
import { Instrument } from '../../../instruments/types/instrument';
import { LedgerService } from '../../ledger-service';
import { isoDate, noBlank, notFuture, positiveAmount, atMostTwoDecimals } from '../../validation-helpers';

type SubmitStatus = 'idle' | 'submitting' | 'error';

type RecordIncomeForm = FormGroup<{
  amount: FormControl<number | null>;
  currency: FormControl<CurrencyCode>;
  targetAccountId: FormControl<string>;
  receivedOn: FormControl<string>;
  description: FormControl<string>;
}>;

@Component({
  selector: 'app-record-income-page',
  imports: [ReactiveFormsModule],
  templateUrl: './record-income-page.html',
  styleUrl: './record-income-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class RecordIncomePage implements OnInit, OnDestroy {
  protected form!: RecordIncomeForm;
  protected readonly maxDate: string = new Date().toISOString().slice(0, 10);
  protected readonly submitStatus: WritableSignal<SubmitStatus> = signal<SubmitStatus>('idle');
  protected readonly submitError: WritableSignal<AppError | null> = signal<AppError | null>(null);
  protected readonly bankAndCashInstruments: Signal<Instrument[]> = computed(() =>
    this.instruments().filter(
      (instrument: Instrument) => instrument.type === 'debit' || instrument.type === 'cash'
    )
  );
  protected readonly errorMessages: Record<string, string> = {
    positiveAmount: 'Enter an amount greater than zero.',
    atMostTwoDecimals: 'Use at most two decimal places.',
    isoDate: 'Use the YYYY-MM-DD format.',
    notFuture: 'The date cannot be in the future.',
    required: 'This field is required.',
    noBlank: 'Enter a description.'
  };

  private readonly fb: FormBuilder = inject(FormBuilder);
  private readonly ledgerService: LedgerService = inject(LedgerService);
  private readonly instrumentsService: InstrumentsService = inject(InstrumentsService);
  private readonly router: Router = inject(Router);
  private readonly instruments: WritableSignal<Instrument[]> = signal<Instrument[]>([]);
  private readonly submitErrorMessages: Record<string, string> = {
    'Ledger.AccountNotFound': 'Choose an account from the list.',
    'Ledger.SourceAccountNotSpendable': 'Pick a bank or cash account to receive the income.',
    'Ledger.InvalidIncomeDescription': 'Enter a description.',
    'Ledger.IncomeDateInFuture': 'The date cannot be in the future.',
    'Http.BadRequest': 'The income could not be recorded — check the values and try again.',
    'Http.UnprocessableEntity': 'Check the amount and date and try again.',
    'Http.ServerError': 'Something went wrong on the server. Try again in a moment.',
    'Http.NetworkError': 'Could not reach the server. Check your connection.'
  };
  private readonly destroy$: Subject<void> = new Subject<void>();

  protected submitErrorText(error: AppError): string {
    return this.submitErrorMessages[error.code] ?? 'The income could not be recorded.';
  }

  protected onSubmit(): void {
    if(this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const raw = this.form.getRawValue();
    const amountMinorUnits: Money = toMinorUnits(raw.amount as number);
    this.submitError.set(null);
    this.submitStatus.set('submitting');
    this.ledgerService
      .recordIncome({
        amountMinorUnits,
        targetAccountId: raw.targetAccountId,
        receivedOn: raw.receivedOn,
        description: raw.description.trim(),
        currencyCode: raw.currency
      })
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => this.router.navigate(['ledger', 'money-flow']),
        error: (error: AppError) => {
          this.submitError.set(error);
          this.submitStatus.set('error');
        }
      }
    );
  }

  private loadInstruments(): void {
    this.instrumentsService
      .list()
      .pipe(takeUntil(this.destroy$))
    .subscribe((rows: Instrument[]) => this.instruments.set(rows));
  }

  private initRecordIncomeForm(): void {
    this.form = this.fb.group({
      amount: this.fb.control<number | null>(null, {
        validators: [positiveAmount, atMostTwoDecimals],
      }),
      currency: this.fb.nonNullable.control<CurrencyCode>('ARS'),
      targetAccountId: this.fb.nonNullable.control('', { validators: Validators.required }),
      receivedOn: this.fb.nonNullable.control(this.maxDate, { validators: [isoDate, notFuture] }),
      description: this.fb.nonNullable.control('', {
        validators: [Validators.required, noBlank]
      })
    });
  }

  ngOnInit(): void {
    this.initRecordIncomeForm();
    this.loadInstruments();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }
}
