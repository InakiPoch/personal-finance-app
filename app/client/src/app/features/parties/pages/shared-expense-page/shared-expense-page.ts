import {
  ChangeDetectionStrategy,
  Component,
  OnDestroy,
  OnInit,
  Signal,
  WritableSignal,
  computed,
  inject,
  signal
} from '@angular/core';
import {
  FormArray,
  FormBuilder,
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  Validators
} from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Subject, takeUntil } from 'rxjs';
import { toMinorUnits } from '../../../../core/money/money';
import { InstrumentRegistryService } from '../../../../core/registry/instrument-registry-service';
import { AppError } from '../../../../core/types/app-error';
import { IsoInstant } from '../../../../core/types/iso-instant';
import { RegisteredInstrument } from '../../../../core/types/registered-instrument';
import { ReportsService } from '../../../reports/reports-service';
import { PartyDebtRow } from '../../../reports/types/party-debt-row';
import { RegisterSharedExpense } from '../../types/register-shared-expense';
import { SharedExpenseParticipant } from '../../types/shared-expense-participant';
import { SharedExpenseResult } from '../../types/shared-expense-result';
import { PartiesService } from '../../parties-service';
import { atMostTwoDecimals, positiveAmount, positiveInteger } from '../../validation-helpers';

type LoadStatus = 'loading' | 'ready' | 'error';
type SubmitStatus = 'idle' | 'submitting' | 'confirmed' | 'error';

type ParticipantRow = FormGroup<{
  partyId: FormControl<string>;
  weight: FormControl<number | null>;
}>;

type SharedExpenseForm = FormGroup<{
  description: FormControl<string>;
  total: FormControl<number | null>;
  expenseAccountId: FormControl<string>;
  fundingAccountId: FormControl<string>;
  incurredOnUtc: FormControl<string>;
  participants: FormArray<ParticipantRow>;
}>;

@Component({
  selector: 'app-shared-expense-page',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './shared-expense-page.html',
  styleUrl: './shared-expense-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class SharedExpensePage implements OnInit, OnDestroy {
  protected form!: SharedExpenseForm;
  protected readonly parties: WritableSignal<PartyDebtRow[]> = signal<PartyDebtRow[]>([]);
  protected readonly partiesStatus: WritableSignal<LoadStatus> = signal<LoadStatus>('loading');
  protected readonly submitStatus: WritableSignal<SubmitStatus> = signal<SubmitStatus>('idle');
  protected readonly submitError: WritableSignal<AppError | null> = signal<AppError | null>(null);
  protected readonly splitReferenceId: WritableSignal<string | null> = signal<string | null>(null);
  protected readonly prefilledPartyId: WritableSignal<string | null> = signal<string | null>(null);
  protected readonly fundingAccounts: Signal<RegisteredInstrument[]> = computed(() =>
    this.registry.instruments()
  );
  protected readonly fieldErrors: Record<string, string> = {
    required: 'This field is required.',
    positiveAmount: 'Enter an amount greater than zero.',
    atMostTwoDecimals: 'Use at most two decimal places.',
    positiveInteger: 'Enter a whole number of at least 1.'
  };

  private readonly route: ActivatedRoute = inject(ActivatedRoute);
  private readonly fb: FormBuilder = inject(FormBuilder);
  private readonly partiesService: PartiesService = inject(PartiesService);
  private readonly reports: ReportsService = inject(ReportsService);
  private readonly registry: InstrumentRegistryService = inject(InstrumentRegistryService);
  private readonly submitErrorMessages: Record<string, string> = {
    'Parties.NonPositiveAmount': 'The total must be greater than zero.',
    'Parties.InvalidParticipants': 'Add at least one participant with a positive weight.',
    'Parties.UnknownFundingAccount': 'Choose a funding account registered with the API.',
    'Parties.PartyNotFound': 'One of the chosen parties no longer exists.',
    'Http.BadRequest': 'The shared expense could not be registered — check the values and try again.',
    'Http.UnprocessableEntity':
      'The API rejected the shared expense — check the total and participants.',
    'Http.ServerError': 'Something went wrong on the server. Try again in a moment.',
    'Http.NetworkError': 'Could not reach the server. Check your connection.'
  };
  private readonly destroy$: Subject<void> = new Subject<void>();

  protected addParticipant(): void {
    this.form.controls.participants.push(this.createParticipantRow(''));
  }

  protected removeParticipant(index: number): void {
    this.form.controls.participants.removeAt(index);
  }

  protected submitErrorText(error: AppError): string {
    return this.submitErrorMessages[error.code] ?? 'The shared expense could not be registered.';
  }

  protected onSubmit(): void {
    if(this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const raw: {
      description: string;
      total: number | null;
      expenseAccountId: string;
      fundingAccountId: string;
      incurredOnUtc: string;
      participants: Array<{ partyId: string; weight: number | null }>;
    } = this.form.getRawValue();
    const participants: SharedExpenseParticipant[] = raw.participants.map((row) => ({
      partyId: row.partyId,
      weight: row.weight as number
    }));
    const body: RegisterSharedExpense = {
      description: raw.description.trim(),
      totalMinorUnits: toMinorUnits(raw.total as number),
      expenseAccountId: raw.expenseAccountId.trim(),
      fundingAccountId: raw.fundingAccountId,
      incurredOnUtc: new Date(raw.incurredOnUtc).toISOString() as IsoInstant,
      participants
    };
    this.submitError.set(null);
    this.splitReferenceId.set(null);
    this.submitStatus.set('submitting');
    this.partiesService
      .registerSharedExpense(body)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (result: SharedExpenseResult) => {
          this.splitReferenceId.set(result.splitReferenceId);
          this.submitStatus.set('confirmed');
        },
        error: (error: AppError) => {
          this.submitError.set(error);
          this.submitStatus.set('error');
        }
      }
    );
  }

  private loadParties(): void {
    this.partiesStatus.set('loading');
    this.reports
      .debtSummary()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (rows: PartyDebtRow[]) => {
          this.parties.set(rows);
          this.partiesStatus.set('ready');
        },
        error: () => this.partiesStatus.set('error')
      }
    );
  }

  private createParticipantRow(partyId: string): ParticipantRow {
    return this.fb.group({
      partyId: this.fb.nonNullable.control(partyId, { validators: Validators.required }),
      weight: this.fb.control<number | null>(1, { validators: positiveInteger })
    });
  }

  private initSharedExpenseForm(): void {
    this.form = this.fb.group({
      description: this.fb.nonNullable.control('', { validators: Validators.required }),
      total: this.fb.control<number | null>(null, {
        validators: [positiveAmount, atMostTwoDecimals]
      }),
      expenseAccountId: this.fb.nonNullable.control('', { validators: Validators.required }),
      fundingAccountId: this.fb.nonNullable.control('', { validators: Validators.required }),
      incurredOnUtc: this.fb.nonNullable.control('', { validators: Validators.required }),
      participants: this.fb.array<ParticipantRow>([])
    });
  }

  ngOnInit(): void {
    this.initSharedExpenseForm();
    this.loadParties();
    const party: string | null = this.route.snapshot.queryParamMap.get('party');
    if(party !== null) {
      this.prefilledPartyId.set(party);
      this.form.controls.participants.push(this.createParticipantRow(party));
    }
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }
}
