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
import { ActivatedRoute, ParamMap, Router, RouterLink } from '@angular/router';
import { Subject, takeUntil } from 'rxjs';
import { formatMoney, fromMinorUnits, toMinorUnits } from '../../../../core/money/money';
import { AppError } from '../../../../core/types/app-error';
import { CurrencyCode } from '../../../../core/types/currency-code';
import { IsoInstant } from '../../../../core/types/iso-instant';
import { Money } from '../../../../core/types/money';
import { InstrumentsService } from '../../../instruments/instruments-service';
import { Instrument } from '../../../instruments/types/instrument';
import { ReportsService } from '../../../reports/reports-service';
import { PartyTimelineRow } from '../../../reports/types/party-timeline-row';
import { CurrentAccountBalance } from '../../types/current-account-balance';
import { FuturePartyShare } from '../../types/future-party-share';
import { PartyCurrencyBalance } from '../../types/party-currency-balance';
import { SettleCurrentAccount } from '../../types/settle-current-account';
import { PartiesService } from '../../parties-service';
import { atMostTwoDecimals, positiveAmount } from '../../validation-helpers';
import { TimelineTable } from './timeline-table';

type LoadStatus = 'loading' | 'ready' | 'error';
type SettleStatus = 'idle' | 'settling' | 'settled' | 'error';

const MONTH_LABELS: readonly string[] = [
  'Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'
];

type SettlementForm = FormGroup<{
  amount: FormControl<number | null>;
  currency: FormControl<CurrencyCode>;
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
  protected readonly formatMoney: (value: Money, code: CurrencyCode) => string = formatMoney;
  protected readonly zeroMinorUnits: Money = fromMinorUnits(0);
  protected readonly balance: WritableSignal<CurrentAccountBalance | null> =
    signal<CurrentAccountBalance | null>(null);
  protected readonly timeline: WritableSignal<PartyTimelineRow[]> = signal<PartyTimelineRow[]>([]);
  protected readonly futureShares: WritableSignal<FuturePartyShare[]> = signal<FuturePartyShare[]>([]);
  protected readonly balanceStatus: WritableSignal<LoadStatus> = signal<LoadStatus>('loading');
  protected readonly timelineStatus: WritableSignal<LoadStatus> = signal<LoadStatus>('loading');
  protected readonly futureSharesStatus: WritableSignal<LoadStatus> = signal<LoadStatus>('loading');
  protected readonly settleStatus: WritableSignal<SettleStatus> = signal<SettleStatus>('idle');
  protected readonly settleError: WritableSignal<AppError | null> = signal<AppError | null>(null);
  protected readonly partyId: WritableSignal<string | null> = signal<string | null>(null);
  protected readonly bankAccounts: Signal<Instrument[]> = computed(() =>
    this.instruments().filter((instrument: Instrument) => instrument.type === 'debit')
  );
  protected readonly owedCurrencies: Signal<CurrencyCode[]> = computed(() => {
    const owed: CurrencyCode[] = (this.balance()?.balances ?? [])
      .filter((row: PartyCurrencyBalance) => row.balanceMinorUnits > 0)
      .map((row: PartyCurrencyBalance) => row.currencyCode);
    return owed.length > 0 ? owed : ['ARS'];
  });
  protected readonly fieldErrors: Record<string, string> = {
    required: 'This field is required.',
    positiveAmount: 'Enter an amount greater than zero.',
    atMostTwoDecimals: 'Use at most two decimal places.'
  };

  private readonly route: ActivatedRoute = inject(ActivatedRoute);
  private readonly router: Router = inject(Router);
  private readonly fb: FormBuilder = inject(FormBuilder);
  private readonly partiesService: PartiesService = inject(PartiesService);
  private readonly reports: ReportsService = inject(ReportsService);
  private readonly instrumentsService: InstrumentsService = inject(InstrumentsService);
  private readonly instruments: WritableSignal<Instrument[]> = signal<Instrument[]>([]);
  private readonly settleErrorMessages: Record<string, string> = {
    'Parties.NonPositiveAmount': 'The settlement amount must be greater than zero.',
    'Parties.UnknownFundingAccount': 'Choose a debit account from the list.',
    'Parties.PartyNotFound': 'This party no longer exists.',
    'Parties.SettlementExceedsBalance': 'The amount is more than what this party owes.',
    'Parties.InvalidCurrencyCode': 'Choose ARS or USD.',
    'Http.BadRequest': 'The settlement could not be recorded — check the values and try again.',
    'Http.UnprocessableEntity': 'Check the amount and account and try again.',
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
    const raw: {
      amount: number | null;
      currency: CurrencyCode;
      bankAccountId: string;
      settledOnUtc: string;
    } = this.form.getRawValue();
    const body: SettleCurrentAccount = {
      amountMinorUnits: toMinorUnits(raw.amount as number),
      currencyCode: raw.currency,
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
          this.form.reset({ amount: null, currency: 'ARS', bankAccountId: '', settledOnUtc: '' });
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

  protected openReverse(transactionId: string): void {
    this.router.navigate(['ledger', 'transactions', transactionId, 'reverse']);
  }

  protected cycleLabel(share: FuturePartyShare): string {
    return `${MONTH_LABELS[share.cycleMonth - 1]} ${share.cycleYear}`;
  }

  private loadInstruments(): void {
    this.instrumentsService
      .list()
      .pipe(takeUntil(this.destroy$))
      .subscribe((rows: Instrument[]) => this.instruments.set(rows));
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

  private loadFutureShares(id: string): void {
    this.futureSharesStatus.set('loading');
    this.partiesService
      .futureShares(id)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (rows: FuturePartyShare[]) => {
          this.futureShares.set(rows);
          this.futureSharesStatus.set('ready');
        },
        error: () => this.futureSharesStatus.set('error')
      }
    );
  }

  private initSettlementForm(): void {
    this.form = this.fb.group({
      amount: this.fb.control<number | null>(null, {
        validators: [positiveAmount, atMostTwoDecimals],
      }),
      currency: this.fb.nonNullable.control<CurrencyCode>('ARS'),
      bankAccountId: this.fb.nonNullable.control('', { validators: Validators.required }),
      settledOnUtc: this.fb.nonNullable.control('', { validators: Validators.required })
    });
  }

  ngOnInit(): void {
    this.initSettlementForm();
    this.loadInstruments();
    this.route.paramMap.pipe(takeUntil(this.destroy$)).subscribe({
      next: (params: ParamMap) => {
        const id: string | null = params.get('id');
        this.partyId.set(id);
        if(id !== null) {
          this.loadBalance(id);
          this.loadTimeline(id);
          this.loadFutureShares(id);
        }
      }
    });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }
}
