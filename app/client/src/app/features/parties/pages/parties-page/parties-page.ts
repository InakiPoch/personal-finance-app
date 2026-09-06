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
import { formatArs, fromMinorUnits } from '../../../../core/money/money';
import { AppError } from '../../../../core/types/app-error';
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

/**
 * Every registered party (roster from `list()`) merged with its balance from `debtSummary()` — zero when
 * the party has no movements yet — and its count of not-yet-accrued scheduled installment shares from
 * `pendingShares()`, so a $0-now party with a schedule reads differently from a truly settled one.
 */
type PartyListRow = {
  partyId: string;
  partyName: string;
  netBalanceMinorUnits: Money;
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
  protected readonly formatArs: (value: Money) => string = formatArs;
  protected readonly listStatus: WritableSignal<ListStatus> = signal<ListStatus>('loading');
  protected readonly parties: WritableSignal<PartyListRow[]> = signal<PartyListRow[]>([]);
  protected readonly totalReceivable: Signal<Money> = computed(() =>
    fromMinorUnits(
      this.parties().reduce(
        (sum: number, row: PartyListRow) =>
          row.netBalanceMinorUnits > 0 ? sum + row.netBalanceMinorUnits : sum,
        0
      )
    )
  );
  protected readonly totalPayable: Signal<Money> = computed(() =>
    fromMinorUnits(
      this.parties().reduce((sum: number, row: PartyListRow) => row.netBalanceMinorUnits < 0 ? sum - row.netBalanceMinorUnits : sum, 0)
    )
  );
  protected readonly maxMagnitude: Signal<number> = computed(() =>
    this.parties().reduce(
      (max: number, row: PartyListRow) => Math.max(max, Math.abs(row.netBalanceMinorUnits)),
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
    if(row.netBalanceMinorUnits > 0) {
      return 'They owe you';
    }
    if(row.netBalanceMinorUnits < 0) {
      return 'You owe them';
    }
    if(row.scheduledCount > 0) {
      return `Nothing owed yet · ${row.scheduledCount} scheduled`;
    }
    return 'Settled up';
  }

  protected submitErrorText(error: AppError): string {
    return this.submitErrorMessages[error.code] ?? 'The party could not be created.';
  }

  /** Width (%) of the magnitude tick behind a party row, relative to the largest |net balance|. */
  protected tickWidth(row: PartyListRow): number {
    const max: number = this.maxMagnitude();
    return max === 0 ? 0 : Math.round((Math.abs(row.netBalanceMinorUnits) / max) * 100);
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
          const balanceByPartyId: Map<string, Money> = new Map(
            debts.map((row: PartyDebtRow) => [row.partyId, row.netBalanceMinorUnits])
          );
          const scheduledCountByPartyId: Map<string, number> = new Map(
            pending.map((row: PendingSharesByPartyRow) => [row.partyId, row.scheduledCount])
          );
          this.parties.set(
            roster.map((party: Party) => ({
              partyId: party.id,
              partyName: party.name,
              netBalanceMinorUnits: balanceByPartyId.get(party.id) ?? fromMinorUnits(0),
              scheduledCount: scheduledCountByPartyId.get(party.id) ?? 0
            }))
          );
          this.listStatus.set('ready');
        },
        error: () => this.listStatus.set('error')
      }
    );
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
