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
import { Subject, map, switchMap, takeUntil } from 'rxjs';
import { pollUntil } from '../../../../core/http/poll-until';
import { formatArs, toMinorUnits } from '../../../../core/money/money';
import { InstrumentRegistryService } from '../../../../core/registry/instrument-registry-service';
import { AppError } from '../../../../core/types/app-error';
import { Money } from '../../../../core/types/money';
import { RegisteredInstrument } from '../../../../core/types/registered-instrument';
import { CurrentAccountBalance } from '../../../parties/types/current-account-balance';
import { PartiesService } from '../../../parties/parties-service';
import { PartyDebtRow } from '../../../reports/types/party-debt-row';
import { ReportsService } from '../../../reports/reports-service';
import { FinancingService } from '../../financing-service';
import { CreatePaymentPlan } from '../../types/create-payment-plan';
import { CreatePaymentPlanResult } from '../../types/create-payment-plan-result';
import { SplitParticipant } from '../../types/split-participant';
import { atMostTwoDecimals, isoDate, positiveAmount, positiveInteger } from '../../validation-helpers';

type LoadStatus = 'loading' | 'ready' | 'error';
type SubmitStatus = 'idle' | 'submitting' | 'confirmed' | 'error';
type ReconciliationStatus = 'reconciling' | 'reconciled' | 'stalled';

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
  cardId: FormControl<string>;
  installmentCount: FormControl<number | null>;
  purchaseDate: FormControl<string>;
  split: FormArray<SplitRow>;
}>;

@Component({
  selector: 'app-load-expense-page',
  imports: [ReactiveFormsModule],
  templateUrl: './load-expense-page.html',
  styleUrl: './load-expense-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LoadExpensePage implements OnInit, OnDestroy {
  protected form!: LoadExpenseForm;
  protected readonly formatArs: (value: Money) => string = formatArs;
  protected readonly parties: WritableSignal<PartyDebtRow[]> = signal<PartyDebtRow[]>([]);
  protected readonly partiesStatus: WritableSignal<LoadStatus> = signal<LoadStatus>('loading');
  protected readonly submitStatus: WritableSignal<SubmitStatus> = signal<SubmitStatus>('idle');
  protected readonly submitError: WritableSignal<AppError | null> = signal<AppError | null>(null);
  protected readonly confirmedPlanId: WritableSignal<string | null> = signal<string | null>(null);
  protected readonly reconciliations: WritableSignal<ReconciliationRow[]> = signal<ReconciliationRow[]>([]);
  protected readonly creditCards: Signal<RegisteredInstrument[]> = computed(() =>
    this.registry.instruments().filter((instrument: RegisteredInstrument) => instrument.type === 'credit')
  );
  protected readonly errorMessages: Record<string, string> = {
    positiveAmount: 'Enter an amount greater than zero.',
    atMostTwoDecimals: 'Use at most two decimal places.',
    positiveInteger: 'Enter a whole number of at least 1.',
    isoDate: 'Use the YYYY-MM-DD format.',
    required: 'This field is required.'
  };

  private readonly fb: FormBuilder = inject(FormBuilder);
  private readonly financingService: FinancingService = inject(FinancingService);
  private readonly partiesService: PartiesService = inject(PartiesService);
  private readonly reportsService: ReportsService = inject(ReportsService);
  private readonly registry: InstrumentRegistryService = inject(InstrumentRegistryService);
  private readonly submitErrorMessages: Record<string, string> = {
    'Financing.CardNotFound': 'That card is not registered with the API yet.',
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

  protected onSubmit(): void {
    if(this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const raw: {
      amount: number | null;
      cardId: string;
      installmentCount: number | null;
      purchaseDate: string;
      split: Array<{ partyId: string; weight: number | null }>;
    } = this.form.getRawValue();
    const participants: SplitParticipant[] = raw.split.map((row) => ({
      partyId: row.partyId,
      weight: row.weight as number,
    }));
    const body: CreatePaymentPlan = {
      amountMinorUnits: toMinorUnits(raw.amount as number),
      cardId: raw.cardId,
      installmentCount: raw.installmentCount as number,
      purchaseDate: raw.purchaseDate,
      ...(participants.length > 0 ? { split: participants } : {}),
    };
    this.submitError.set(null);
    this.confirmedPlanId.set(null);
    this.reconciliations.set([]);
    this.submitStatus.set('submitting');
    this.financingService
      .createPaymentPlan(body)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (result: CreatePaymentPlanResult) => {
          this.confirmedPlanId.set(result.paymentPlanId);
          this.submitStatus.set('confirmed');
          if(participants.length > 0) {
            this.reconcile(participants);
          }
        },
        error: (error: AppError) => {
          this.submitError.set(error);
          this.submitStatus.set('error');
        },
      }
    );
  }

  private reconcile(participants: SplitParticipant[]): void {
    this.reconciliations.set(
      participants.map((participant: SplitParticipant) => ({
        partyId: participant.partyId,
        partyName: this.partyName(participant.partyId),
        status: 'reconciling' as ReconciliationStatus,
        balanceMinorUnits: null,
      })),
    );
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
          takeUntil(this.destroy$),
        )
        .subscribe({
          next: (balance: CurrentAccountBalance) =>
            this.updateReconciliation(participant.partyId, 'reconciled', balance.balanceMinorUnits),
          error: () => this.updateReconciliation(participant.partyId, 'stalled', null),
        }
      );
    }
  }

  private updateReconciliation(partyId: string, status: ReconciliationStatus, balanceMinorUnits: Money | null): void {
    this.reconciliations.update((rows: ReconciliationRow[]) =>
      rows.map((row: ReconciliationRow) =>
        row.partyId === partyId ? { ...row, status, balanceMinorUnits } : row,
      ),
    );
  }

  private partyName(partyId: string): string {
    return this.parties().find((row: PartyDebtRow) => row.partyId === partyId)?.partyName ?? partyId;
  }

  private loadParties(): void {
    this.partiesStatus.set('loading');
    this.reportsService
      .debtSummary()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (rows: PartyDebtRow[]) => {
          this.parties.set(rows);
          this.partiesStatus.set('ready');
        },
        error: () => this.partiesStatus.set('error'),
      }
    );
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
      cardId: this.fb.nonNullable.control('', { validators: Validators.required }),
      installmentCount: this.fb.control<number | null>(1, { validators: positiveInteger }),
      purchaseDate: this.fb.nonNullable.control('', { validators: isoDate }),
      split: this.fb.array<SplitRow>([]),
    });
  }

  ngOnInit(): void {
    this.initLoadExpenseForm();
    this.loadParties();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }
}
