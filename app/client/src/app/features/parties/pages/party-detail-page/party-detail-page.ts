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
import { ActivatedRoute, ParamMap, RouterLink } from '@angular/router';
import { Subject, takeUntil } from 'rxjs';
import { formatArs, toMinorUnits } from '../../../../core/money/money';
import { InstrumentRegistryService } from '../../../../core/registry/instrument-registry-service';
import { AppError } from '../../../../core/types/app-error';
import { IsoInstant } from '../../../../core/types/iso-instant';
import { Money } from '../../../../core/types/money';
import { RegisteredInstrument } from '../../../../core/types/registered-instrument';
import { ReportsService } from '../../../reports/reports-service';
import { PartyTimelineRow } from '../../../reports/types/party-timeline-row';
import { CurrentAccountBalance } from '../../types/current-account-balance';
import { SettleCurrentAccount } from '../../types/settle-current-account';
import { PartiesService } from '../../parties-service';
import { atMostTwoDecimals, positiveAmount } from '../../validation-helpers';
import { TimelineTable } from './timeline-table';

type LoadStatus = 'loading' | 'ready' | 'error';
type SettleStatus = 'idle' | 'settling' | 'settled' | 'error';

type SettlementForm = FormGroup<{
  amount: FormControl<number | null>;
  bankAccountId: FormControl<string>;
  settledOnUtc: FormControl<string>;
}>;

@Component({
  selector: 'app-party-detail-page',
  imports: [ReactiveFormsModule, RouterLink, TimelineTable],
  templateUrl: './party-detail-page.html',
  styleUrl: './party-detail-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class PartyDetailPage implements OnInit, OnDestroy {
  protected form!: SettlementForm;
  protected readonly formatArs: (value: Money) => string = formatArs;
  protected readonly balance: WritableSignal<CurrentAccountBalance | null> =
    signal<CurrentAccountBalance | null>(null);
  protected readonly timeline: WritableSignal<PartyTimelineRow[]> = signal<PartyTimelineRow[]>([]);
  protected readonly balanceStatus: WritableSignal<LoadStatus> = signal<LoadStatus>('loading');
  protected readonly timelineStatus: WritableSignal<LoadStatus> = signal<LoadStatus>('loading');
  protected readonly settleStatus: WritableSignal<SettleStatus> = signal<SettleStatus>('idle');
  protected readonly settleError: WritableSignal<AppError | null> = signal<AppError | null>(null);
  protected readonly partyId: WritableSignal<string | null> = signal<string | null>(null);
  protected readonly bankAccounts: Signal<RegisteredInstrument[]> = computed(() =>
    this.registry.instruments().filter((instrument: RegisteredInstrument) => instrument.type === 'debit')
  );
  protected readonly fieldErrors: Record<string, string> = {
    required: 'This field is required.',
    positiveAmount: 'Enter an amount greater than zero.',
    atMostTwoDecimals: 'Use at most two decimal places.'
  };

  private readonly route: ActivatedRoute = inject(ActivatedRoute);
  private readonly fb: FormBuilder = inject(FormBuilder);
  private readonly partiesService: PartiesService = inject(PartiesService);
  private readonly reports: ReportsService = inject(ReportsService);
  private readonly registry: InstrumentRegistryService = inject(InstrumentRegistryService);
  private readonly settleErrorMessages: Record<string, string> = {
    'Parties.NonPositiveAmount': 'The settlement amount must be greater than zero.',
    'Parties.UnknownFundingAccount': 'Choose a debit account registered with the API.',
    'Parties.PartyNotFound': 'This party no longer exists.',
    'Parties.SettlementExceedsBalance': 'The amount is more than what this party owes.',
    'Http.BadRequest': 'The settlement could not be recorded — check the values and try again.',
    'Http.UnprocessableEntity': 'The API rejected the settlement — check the amount and account.',
    'Http.Conflict': 'The amount is more than what this party owes.',
    'Http.ServerError': 'Something went wrong on the server. Try again in a moment.',
    'Http.NetworkError': 'Could not reach the server. Check your connection.'
  };
  private readonly destroy$: Subject<void> = new Subject<void>();

  protected onSubmit(): void {
    const id: string | null = this.partyId();
    if(this.form.invalid || id === null) {
      this.form.markAllAsTouched();
      return;
    }
    const raw: { amount: number | null; bankAccountId: string; settledOnUtc: string } =
      this.form.getRawValue();
    const body: SettleCurrentAccount = {
      amountMinorUnits: toMinorUnits(raw.amount as number),
      bankAccountId: raw.bankAccountId,
      settledOnUtc: new Date(raw.settledOnUtc).toISOString() as IsoInstant
    };
    this.settleError.set(null);
    this.settleStatus.set('settling');
    this.partiesService
      .settle(id, body)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.settleStatus.set('settled');
          this.form.reset({ amount: null, bankAccountId: '', settledOnUtc: '' });
          this.loadBalance(id);
          this.loadTimeline(id);
        },
        error: (error: AppError) => {
          this.settleError.set(error);
          this.settleStatus.set('error');
        }
      }
    );
  }

  protected settleErrorText(error: AppError): string {
    return this.settleErrorMessages[error.code] ?? 'The settlement could not be recorded.';
  }

  private loadBalance(id: string): void {
    this.balanceStatus.set('loading');
    this.partiesService
      .getBalance(id)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (balance: CurrentAccountBalance) => {
          this.balance.set(balance);
          this.balanceStatus.set('ready');
        },
        error: () => this.balanceStatus.set('error')
      }
    );
  }

  private loadTimeline(id: string): void {
    this.timelineStatus.set('loading');
    this.reports
      .partyTimeline(id)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (rows: PartyTimelineRow[]) => {
          this.timeline.set(rows);
          this.timelineStatus.set('ready');
        },
        error: () => this.timelineStatus.set('error')
      }
    );
  }

  private initSettlementForm(): void {
    this.form = this.fb.group({
      amount: this.fb.control<number | null>(null, {
        validators: [positiveAmount, atMostTwoDecimals],
      }),
      bankAccountId: this.fb.nonNullable.control('', { validators: Validators.required }),
      settledOnUtc: this.fb.nonNullable.control('', { validators: Validators.required })
    });
  }

  ngOnInit(): void {
    this.initSettlementForm();
    this.route.paramMap.pipe(takeUntil(this.destroy$)).subscribe({
      next: (params: ParamMap) => {
        const id: string | null = params.get('id');
        this.partyId.set(id);
        if(id !== null) {
          this.loadBalance(id);
          this.loadTimeline(id);
        }
      }
    });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }
}
