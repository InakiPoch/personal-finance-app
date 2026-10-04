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
import { RouterLink } from '@angular/router';
import { Subject, forkJoin, takeUntil } from 'rxjs';
import { formatMoney, fromMinorUnits } from '../../../../core/money/money';
import { AppError } from '../../../../core/types/app-error';
import { CurrencyCode } from '../../../../core/types/currency-code';
import { Money } from '../../../../core/types/money';
import { ReportsService } from '../../../reports/reports-service';
import { PartyDebtRow } from '../../../reports/types/party-debt-row';
import { CreateParty } from '../../types/create-party';
import { Party } from '../../types/party';
import { PartyResult } from '../../types/party-result';
import { PendingSharesByPartyRow } from '../../types/pending-shares-by-party-row';
import { PartiesService } from '../../parties-service';

type ListStatus = 'loading' | 'ready' | 'error';
type SubmitStatus = 'idle' | 'submitting' | 'error';

type PartyForm = FormGroup<{
  name: FormControl<string>;
}>;

/** One party's net position in one currency. */
type PartyCurrencyNet = {
  currencyCode: CurrencyCode;
  netBalanceMinorUnits: Money;
};

type PartyListRow = {
  partyId: string;
  partyName: string;
  balances: PartyCurrencyNet[];
  scheduledCount: number;
};

@Component({
  selector: 'app-parties-page',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './parties-page.html',
  styleUrl: './parties-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PartiesPage implements OnInit, OnDestroy {
  protected form!: PartyForm;
  protected readonly formatMoney: (value: Money, code: CurrencyCode) => string = formatMoney;
  protected readonly zeroMinorUnits: Money = fromMinorUnits(0);
  protected readonly listStatus: WritableSignal<ListStatus> = signal<ListStatus>('loading');
  protected readonly parties: WritableSignal<PartyListRow[]> = signal<PartyListRow[]>([]);
  protected readonly receivableTotals: Signal<PartyCurrencyNet[]> = computed(() =>
    this.sumByCurrency((net: number) => net > 0)
  );
  protected readonly payableTotals: Signal<PartyCurrencyNet[]> = computed(() =>
    this.sumByCurrency((net: number) => net < 0).map((total: PartyCurrencyNet) => ({
      ...total,
      netBalanceMinorUnits: fromMinorUnits(-total.netBalanceMinorUnits)
    }))
  );
  protected readonly maxMagnitude: Signal<number> = computed(() =>
    this.parties().reduce(
      (max: number, row: PartyListRow) =>
        row.balances.reduce(
          (rowMax: number, balance: PartyCurrencyNet) => Math.max(rowMax, Math.abs(balance.netBalanceMinorUnits)),
          max
        ),
      0
    )
  );
  protected readonly submitStatus: WritableSignal<SubmitStatus> = signal<SubmitStatus>('idle');
  protected readonly submitError: WritableSignal<AppError | null> = signal<AppError | null>(null);
  protected readonly createdPartyId: WritableSignal<string | null> = signal<string | null>(null);
  protected readonly fieldErrors: Record<string, string> = {
    required: 'This field is required.'
  };

  private readonly fb: FormBuilder = inject(FormBuilder);
  private readonly partiesService: PartiesService = inject(PartiesService);
  private readonly reports: ReportsService = inject(ReportsService);
  private readonly submitErrorMessages: Record<string, string> = {
    'Parties.InvalidName': 'Enter a name for the party.',
    'Http.UnprocessableEntity': 'The party could not be created — check the name and try again.',
    'Http.ServerError': 'Something went wrong on the server. Try again in a moment.',
    'Http.NetworkError': 'Could not reach the server. Check your connection.'
  };
  private readonly destroy$: Subject<void> = new Subject<void>();

  protected onSubmit(): void {
    if(this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const raw: { name: string } = this.form.getRawValue();
    const body: CreateParty = { name: raw.name.trim() };
    this.submitError.set(null);
    this.submitStatus.set('submitting');
    this.partiesService
      .create(body)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (result: PartyResult) => {
          this.createdPartyId.set(result.id);
          this.form.reset({ name: '' });
          this.submitStatus.set('idle');
          this.loadParties();
        },
        error: (error: AppError) => {
          this.submitError.set(error);
          this.submitStatus.set('error');
        }
      }
    );
  }

  protected balanceHint(row: PartyListRow): string {
    if(row.balances.length === 0) {
      return row.scheduledCount > 0 ? `Nothing owed yet · ${row.scheduledCount} scheduled` : 'Settled up';
    }
    const hints: string[] = Array.from(
      new Set(row.balances.map((balance: PartyCurrencyNet) => this.currencyHint(balance)))
    );
    return hints.join(' · ');
  }

  protected currencyHint(balance: PartyCurrencyNet): string {
    if(balance.netBalanceMinorUnits > 0) {
      return 'They owe you';
    }
    if(balance.netBalanceMinorUnits < 0) {
      return 'You owe them';
    }
    return 'Settled up';
  }

  protected submitErrorText(error: AppError): string {
    return this.submitErrorMessages[error.code] ?? 'The party could not be created.';
  }

  /** Width (%) of the magnitude tick behind a party row, relative to the largest |net balance| across every currency. */
  protected tickWidth(row: PartyListRow): number {
    const max: number = this.maxMagnitude();
    if(max === 0 || row.balances.length === 0) {
      return 0;
    }
    const rowMax: number = row.balances.reduce(
      (running: number, balance: PartyCurrencyNet) => Math.max(running, Math.abs(balance.netBalanceMinorUnits)),
      0
    );
    return Math.round((rowMax / max) * 100);
  }

  private loadParties(): void {
    this.listStatus.set('loading');
    forkJoin({
      roster: this.partiesService.list(),
      debts: this.reports.debtSummary(),
      pending: this.partiesService.pendingShares()
    })
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: ({ roster, debts, pending }: { roster: Party[]; debts: PartyDebtRow[]; pending: PendingSharesByPartyRow[] }) => {
          const balancesByPartyId: Map<string, PartyCurrencyNet[]> = new Map();
          for(const row of debts) {
            const existing: PartyCurrencyNet[] = balancesByPartyId.get(row.partyId) ?? [];
            existing.push({ currencyCode: row.currencyCode, netBalanceMinorUnits: row.netBalanceMinorUnits });
            balancesByPartyId.set(row.partyId, existing);
          }
          const scheduledCountByPartyId: Map<string, number> = new Map(
            pending.map((row: PendingSharesByPartyRow) => [row.partyId, row.scheduledCount])
          );
          this.parties.set(
            roster.map((party: Party) => ({
              partyId: party.id,
              partyName: party.name,
              balances: balancesByPartyId.get(party.id) ?? [],
              scheduledCount: scheduledCountByPartyId.get(party.id) ?? 0
            }))
          );
          this.listStatus.set('ready');
        },
        error: () => this.listStatus.set('error')
      }
    );
  }

  /** Sums every party's currency balances matching `matches`, one total per currency. */
  private sumByCurrency(matches: (net: number) => boolean): PartyCurrencyNet[] {
    const totals: Map<CurrencyCode, number> = new Map();
    for(const party of this.parties()) {
      for(const balance of party.balances) {
        if(!matches(balance.netBalanceMinorUnits)) {
          continue;
        }
        totals.set(balance.currencyCode, (totals.get(balance.currencyCode) ?? 0) + balance.netBalanceMinorUnits);
      }
    }
    return Array.from(totals.entries()).map(([currencyCode, netBalanceMinorUnits]) => ({
      currencyCode,
      netBalanceMinorUnits: fromMinorUnits(netBalanceMinorUnits)
    }));
  }

  private initPartyForm(): void {
    this.form = this.fb.group({
      name: this.fb.nonNullable.control('', { validators: Validators.required })
    });
  }

  ngOnInit(): void {
    this.initPartyForm();
    this.loadParties();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }
}
