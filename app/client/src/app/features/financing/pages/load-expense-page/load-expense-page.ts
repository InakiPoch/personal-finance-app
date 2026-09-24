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
import { FormArray, FormBuilder, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Observable, Subject, map, merge, switchMap, takeUntil } from 'rxjs';
import { pollUntil } from '../../../../core/http/poll-until';
import { formatArs, toMinorUnits } from '../../../../core/money/money';
import { AppError } from '../../../../core/types/app-error';
import { CurrencyCode } from '../../../../core/types/currency-code';
import { Money } from '../../../../core/types/money';
import { InstrumentsService } from '../../../instruments/instruments-service';
import { Instrument } from '../../../instruments/types/instrument';
import { CreditorsService } from '../../../creditors/creditors-service';
import { Creditor, CreditorAccount } from '../../../creditors/types/creditor';
import { LedgerService } from '../../../ledger/ledger-service';
import { RecordDebitExpenseResult } from '../../../ledger/types/record-debit-expense-result';
import { CurrentAccountBalance } from '../../../parties/types/current-account-balance';
import { Party } from '../../../parties/types/party';
import { PartiesService } from '../../../parties/parties-service';
import { FinancingService } from '../../financing-service';
import { CreatePaymentPlanResult } from '../../types/create-payment-plan-result';
import { SplitParticipant } from '../../types/split-participant';
import {
  atMostTwoDecimals,
  isoDate,
  noBlank,
  noNewline,
  notFuture,
  positiveAmount,
  positiveInteger,
} from '../../validation-helpers';

type LoadStatus = 'loading' | 'ready' | 'error';
type SubmitStatus = 'idle' | 'submitting' | 'confirmed' | 'error';
type ReconciliationStatus = 'reconciling' | 'reconciled' | 'stalled' | 'scheduled';
type LoadExpenseMode = 'card' | 'creditor' | 'debit';
type ConfirmedKind = 'plan' | 'expense';

type ModeOption = { value: LoadExpenseMode; label: string };

type ReconciliationRow = {
  partyId: string;
  partyName: string;
  status: ReconciliationStatus;
  balanceMinorUnits: Money | null;
};

type SplitRow = FormGroup<{
  partyId: FormControl<string>;
  weight: FormControl<number | null>;
}>;

type LoadExpenseForm = FormGroup<{
  amount: FormControl<number | null>;
  currency: FormControl<CurrencyCode>;
  cardId: FormControl<string>;
  installmentCount: FormControl<number | null>;
  purchaseDate: FormControl<string>;
  description: FormControl<string>;
  split: FormArray<SplitRow>;
  mode: FormControl<LoadExpenseMode>;
  creditorId: FormControl<string>;
  creditorAccountId: FormControl<string>;
  bankAccountId: FormControl<string>;
  sourceInstrumentId: FormControl<string>;
  categoryName: FormControl<string>;
}>;

@Component({
  selector: 'app-load-expense-page',
  imports: [ReactiveFormsModule],
  templateUrl: './load-expense-page.html',
  styleUrl: './load-expense-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class LoadExpensePage implements OnInit, OnDestroy {
  protected form!: LoadExpenseForm;
  protected readonly formatArs: (value: Money) => string = formatArs;
  protected readonly parties: WritableSignal<Party[]> = signal<Party[]>([]);
  protected readonly partiesStatus: WritableSignal<LoadStatus> = signal<LoadStatus>('loading');
  protected readonly submitStatus: WritableSignal<SubmitStatus> = signal<SubmitStatus>('idle');
  protected readonly submitError: WritableSignal<AppError | null> = signal<AppError | null>(null);
  protected readonly confirmedPlanId: WritableSignal<string | null> = signal<string | null>(null);
  protected readonly confirmedDescription: WritableSignal<string | null> = signal<string | null>(null);
  protected readonly confirmedKind: WritableSignal<ConfirmedKind> = signal<ConfirmedKind>('plan');
  protected readonly reconciliations: WritableSignal<ReconciliationRow[]> = signal<ReconciliationRow[]>([]);
  protected readonly creditCards: Signal<Instrument[]> = computed(() =>
    this.instruments().filter((instrument: Instrument) => instrument.type === 'credit')
  );
  protected readonly bankAndCashInstruments: Signal<Instrument[]> = computed(() =>
    this.instruments().filter(
      (instrument: Instrument) => instrument.type === 'debit' || instrument.type === 'cash'
    )
  );
  protected readonly expenseCategories: WritableSignal<string[]> = signal<string[]>([]);
  protected readonly creditors: WritableSignal<Creditor[]> = signal<Creditor[]>([]);
  protected readonly creditorAccounts: WritableSignal<CreditorAccount[]> = signal<CreditorAccount[]>([]);
  protected readonly modeOptions: readonly ModeOption[] = [
    { value: 'card', label: 'My credit card' },
    { value: 'debit', label: 'My debit-cash' },
    { value: 'creditor', label: 'Financed by a creditor' },
  ];
  protected readonly errorMessages: Record<string, string> = {
    positiveAmount: 'Enter an amount greater than zero.',
    atMostTwoDecimals: 'Use at most two decimal places.',
    positiveInteger: 'Enter a whole number of at least 1.',
    isoDate: 'Use the YYYY-MM-DD format.',
    notFuture: 'The purchase date cannot be in the future.',
    required: 'This field is required.',
    noBlank: 'Enter a description.',
    noNewline: 'Use a single line.',
    maxlength: 'Keep it under 120 characters.'
  };

  private readonly fb: FormBuilder = inject(FormBuilder);
  private readonly financingService: FinancingService = inject(FinancingService);
  private readonly ledgerService: LedgerService = inject(LedgerService);
  private readonly creditorsService: CreditorsService = inject(CreditorsService);
  private readonly partiesService: PartiesService = inject(PartiesService);
  private readonly instrumentsService: InstrumentsService = inject(InstrumentsService);
  private readonly instruments: WritableSignal<Instrument[]> = signal<Instrument[]>([]);
  private readonly submitErrorMessages: Record<string, string> = {
    'Financing.CardNotFound': 'That card is not registered with the API yet.',
    'Financing.FuturePurchaseDate': 'The purchase date cannot be in the future.',
    'Financing.BackdatedCardBankAccountRequired': 'Pick an account to settle the already-due installments from.',
    'Ledger.AccountNotFound': 'That account is not registered with the API.',
    'Ledger.SourceAccountNotSpendable': 'Pick a debit or cash account to pay from.',
    'Ledger.InvalidExpenseCategory': 'Enter a category for the expense.',
    'Http.BadRequest': 'The expense could not be loaded — check the values and try again.',
    'Http.UnprocessableEntity': 'The API rejected the expense — check the amount and dates.',
    'Http.ServerError': 'Something went wrong on the server. Try again in a moment.',
    'Http.NetworkError': 'Could not reach the server. Check your connection.'
  };
  private readonly destroy$: Subject<void> = new Subject<void>();

  protected addSplitRow(): void {
    this.form.controls.split.push(this.createSplitRow());
  }

  protected removeSplitRow(index: number): void {
    this.form.controls.split.removeAt(index);
  }

  protected submitErrorText(error: AppError): string {
    return this.submitErrorMessages[error.code] ?? 'The expense could not be loaded.';
  }

  protected isBackdatedCardPurchase(): boolean {
    const purchaseDate: string = this.form.controls.purchaseDate.value;
    return (
      this.form.controls.mode.value === 'card' &&
      /^\d{4}-\d{2}-\d{2}$/.test(purchaseDate) &&
      purchaseDate < this.todayIso()
    );
  }

  protected onSubmit(): void {
    if(this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const raw = this.form.getRawValue();
    const participants: SplitParticipant[] = raw.split.map((row) => ({
      partyId: row.partyId,
      weight: row.weight as number,
    }));
    const amountMinorUnits: Money = toMinorUnits(raw.amount as number);
    const description: string = raw.description.trim();
    const split: { split?: SplitParticipant[] } = participants.length > 0 ? { split: participants } : {};
    const request$: Observable<string> =
      raw.mode === 'debit'
        ? this.ledgerService
            .recordDebitExpense({
              amountMinorUnits,
              sourceInstrumentId: raw.sourceInstrumentId,
              categoryName: raw.categoryName.trim(),
              purchaseDate: raw.purchaseDate,
              description,
              currencyCode: raw.currency,
              ...split
            })
            .pipe(map((result: RecordDebitExpenseResult) => result.id))
        : this.financingService
            .createPaymentPlan({
              amountMinorUnits,
              installmentCount: raw.installmentCount as number,
              purchaseDate: raw.purchaseDate,
              description,
              currencyCode: raw.currency,
              ...(raw.mode === 'card'
                ? {
                    cardId: raw.cardId,
                    ...(raw.bankAccountId ? { bankAccountId: raw.bankAccountId } : {}),
                  }
                : { creditorId: raw.creditorId, creditorAccountId: raw.creditorAccountId }),
              ...split
            })
            .pipe(map((result: CreatePaymentPlanResult) => result.paymentPlanId));
    this.submitError.set(null);
    this.confirmedPlanId.set(null);
    this.confirmedDescription.set(null);
    this.confirmedKind.set(raw.mode === 'debit' ? 'expense' : 'plan');
    this.reconciliations.set([]);
    this.submitStatus.set('submitting');
    request$.pipe(takeUntil(this.destroy$)).subscribe({
      next: (id: string) => {
        this.confirmedPlanId.set(id);
        this.confirmedDescription.set(description);
        this.submitStatus.set('confirmed');
        if(participants.length > 0) {
          this.reconcile(participants, raw.mode);
        }
      },
      error: (error: AppError) => {
        this.submitError.set(error);
        this.submitStatus.set('error');
      },
    });
  }

  private reconcile(participants: SplitParticipant[], mode?: LoadExpenseMode): void {
    this.reconciliations.set(
      participants.map((participant: SplitParticipant) => ({
        partyId: participant.partyId,
        partyName: this.partyName(participant.partyId),
        status: 'reconciling' as ReconciliationStatus,
        balanceMinorUnits: null
      }))
    );
    if(mode === 'card' || mode === 'creditor') {
      participants.forEach((participant: SplitParticipant) =>
        this.updateReconciliation(participant.partyId, 'scheduled', null)
      );
      return;
    }
    for(const participant of participants) {
      this.partiesService
        .getBalance(participant.partyId)
        .pipe(
          map((snapshot: CurrentAccountBalance) => snapshot.balanceMinorUnits),
          switchMap((prior: Money) =>
            pollUntil(
              () => this.partiesService.getBalance(participant.partyId),
              (balance: CurrentAccountBalance) => balance.balanceMinorUnits !== prior,
              { intervalMs: 800, maxAttempts: 5 },
            ),
          ),
          takeUntil(this.destroy$)
        )
        .subscribe({
          next: (balance: CurrentAccountBalance) =>
            this.updateReconciliation(participant.partyId, 'reconciled', balance.balanceMinorUnits),
          error: () => this.updateReconciliation(participant.partyId, 'stalled', null)
        }
      );
    }
  }

  private updateReconciliation(partyId: string, status: ReconciliationStatus, balanceMinorUnits: Money | null): void {
    this.reconciliations.update((rows: ReconciliationRow[]) =>
      rows.map((row: ReconciliationRow) =>
        row.partyId === partyId ? { ...row, status, balanceMinorUnits } : row
      )
    );
  }

  private partyName(partyId: string): string {
    return this.parties().find((row: Party) => row.id === partyId)?.name ?? partyId;
  }

  private loadParties(): void {
    this.partiesStatus.set('loading');
    this.partiesService
      .list()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (rows: Party[]) => {
          this.parties.set(rows);
          this.partiesStatus.set('ready');
        },
        error: () => this.partiesStatus.set('error')
      }
    );
  }

  private loadInstruments(): void {
    this.instrumentsService
      .list()
      .pipe(takeUntil(this.destroy$))
    .subscribe((rows: Instrument[]) => this.instruments.set(rows));
  }

  private loadCreditors(): void {
    this.creditorsService
      .list()
      .pipe(takeUntil(this.destroy$))
      .subscribe((rows: Creditor[]) => this.creditors.set(rows));
  }

  private loadExpenseCategories(): void {
    this.ledgerService
      .listExpenseCategories()
      .pipe(takeUntil(this.destroy$))
      .subscribe((names: string[]) => this.expenseCategories.set(names));
  }

  private watchModeChange(): void {
    this.form.controls.mode.valueChanges.pipe(takeUntil(this.destroy$)).subscribe((mode: LoadExpenseMode) => {
      const cardIdControl = this.form.controls.cardId;
      const creditorIdControl = this.form.controls.creditorId;
      const creditorAccountIdControl = this.form.controls.creditorAccountId;
      const sourceInstrumentIdControl = this.form.controls.sourceInstrumentId;
      const categoryNameControl = this.form.controls.categoryName;
      const installmentCountControl = this.form.controls.installmentCount;
      cardIdControl.setValidators(mode === 'card' ? Validators.required : null);
      creditorIdControl.setValidators(mode === 'creditor' ? Validators.required : null);
      creditorAccountIdControl.setValidators(mode === 'creditor' ? Validators.required : null);
      sourceInstrumentIdControl.setValidators(mode === 'debit' ? Validators.required : null);
      categoryNameControl.setValidators(mode === 'debit' ? [Validators.required, noBlank] : null);
      installmentCountControl.setValidators(mode === 'debit' ? null : positiveInteger);
      if(mode !== 'card') {
        cardIdControl.setValue('');
      }
      if(mode !== 'creditor') {
        creditorIdControl.setValue('');
        creditorAccountIdControl.setValue('');
        this.creditorAccounts.set([]);
      }
      if(mode === 'debit') {
        installmentCountControl.setValue(1);
      } else {
        sourceInstrumentIdControl.setValue('');
        categoryNameControl.setValue('');
      }
      cardIdControl.updateValueAndValidity();
      creditorIdControl.updateValueAndValidity();
      creditorAccountIdControl.updateValueAndValidity();
      sourceInstrumentIdControl.updateValueAndValidity();
      categoryNameControl.updateValueAndValidity();
      installmentCountControl.updateValueAndValidity();
    });
  }

  private todayIso(): string {
    return new Date().toISOString().slice(0, 10);
  }

  private watchBackdatedFunding(): void {
    merge(this.form.controls.mode.valueChanges, this.form.controls.purchaseDate.valueChanges)
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => {
        const control: FormControl<string> = this.form.controls.bankAccountId;
        if(this.isBackdatedCardPurchase()) {
          control.setValidators(Validators.required);
        } else {
          control.setValidators(null);
          control.setValue('', { emitEvent: false });
        }
        control.updateValueAndValidity({ emitEvent: false });
      });
  }

  private watchCreditorSelection(): void {
    this.form.controls.creditorId.valueChanges.pipe(takeUntil(this.destroy$)).subscribe((creditorId: string) => {
      const accounts: CreditorAccount[] =
        this.creditors().find((candidate: Creditor) => candidate.id === creditorId)?.accounts ?? [];
      this.creditorAccounts.set(accounts);
      this.form.controls.creditorAccountId.setValue(accounts.length > 0 ? accounts[0].id : '');
    });
  }

  private createSplitRow(): SplitRow {
    return this.fb.group({
      partyId: this.fb.nonNullable.control('', { validators: Validators.required }),
      weight: this.fb.control<number | null>(1, { validators: positiveInteger }),
    });
  }

  private initLoadExpenseForm(): void {
    this.form = this.fb.group({
      amount: this.fb.control<number | null>(null, {
        validators: [positiveAmount, atMostTwoDecimals],
      }),
      currency: this.fb.nonNullable.control<CurrencyCode>('ARS'),
      cardId: this.fb.nonNullable.control('', { validators: Validators.required }),
      installmentCount: this.fb.control<number | null>(1, { validators: positiveInteger }),
      purchaseDate: this.fb.nonNullable.control('', { validators: [isoDate, notFuture] }),
      description: this.fb.nonNullable.control('', {
        validators: [Validators.required, Validators.maxLength(120), noBlank, noNewline],
      }),
      split: this.fb.array<SplitRow>([]),
      mode: this.fb.nonNullable.control<LoadExpenseMode>('card'),
      creditorId: this.fb.nonNullable.control(''),
      creditorAccountId: this.fb.nonNullable.control(''),
      bankAccountId: this.fb.nonNullable.control(''),
      sourceInstrumentId: this.fb.nonNullable.control(''),
      categoryName: this.fb.nonNullable.control('')
    });
  }

  ngOnInit(): void {
    this.initLoadExpenseForm();
    this.loadInstruments();
    this.loadParties();
    this.loadCreditors();
    this.loadExpenseCategories();
    this.watchModeChange();
    this.watchBackdatedFunding();
    this.watchCreditorSelection();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }
}
