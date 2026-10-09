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
import { Subject, takeUntil } from 'rxjs';
import { formatMoney, fromMinorUnits } from '../../../../core/money/money';
import { AppError } from '../../../../core/types/app-error';
import { CurrencyCode } from '../../../../core/types/currency-code';
import { Money } from '../../../../core/types/money';
import { CreateParty } from '../../types/create-party';
import { PartyCurrencyBalance } from '../../types/party-currency-balance';
import { PartyResult } from '../../types/party-result';
import { PartySummary } from '../../types/party-summary';
import { PartiesService } from '../../parties-service';

type ListStatus = 'loading' | 'ready' | 'error';
type SubmitStatus = 'idle' | 'submitting' | 'error';

type PartyForm = FormGroup<{
  name: FormControl<string>;
}>;

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
  protected readonly parties: WritableSignal<PartySummary[]> = signal<PartySummary[]>([]);
  protected readonly receivableTotals: Signal<PartyCurrencyBalance[]> = computed(() =>
    this.sumByCurrency((row: PartySummary) => row.owedToYou)
  );
  protected readonly payableTotals: Signal<PartyCurrencyBalance[]> = computed(() =>
    this.sumByCurrency((row: PartySummary) => row.youOwe)
  );
  protected readonly maxMagnitude: Signal<number> = computed(() =>
    this.parties().reduce(
      (max: number, row: PartySummary) =>
        [...row.owedToYou, ...row.youOwe].reduce(
          (rowMax: number, balance: PartyCurrencyBalance) => Math.max(rowMax, balance.balanceMinorUnits),
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

  protected balanceHint(row: PartySummary): string {
    if(row.settledUp) {
      return 'Settled up';
    }
    const scheduled: number = row.scheduledToYouCount + row.scheduledYouOweCount;
    if(row.owedToYou.length === 0 && row.youOwe.length === 0) {
      return `Nothing owed yet · ${scheduled} scheduled`;
    }
    return scheduled > 0 ? `${scheduled} scheduled` : '';
  }

  protected submitErrorText(error: AppError): string {
    return this.submitErrorMessages[error.code] ?? 'The party could not be created.';
  }

  /** Width (%) of the magnitude tick behind a party row. */
  protected tickWidth(row: PartySummary): number {
    const max: number = this.maxMagnitude();
    if(max === 0) {
      return 0;
    }
    const rowMax: number = [...row.owedToYou, ...row.youOwe].reduce(
      (running: number, balance: PartyCurrencyBalance) => Math.max(running, balance.balanceMinorUnits),
      0
    );
    return Math.round((rowMax / max) * 100);
  }

  private loadParties(): void {
    this.listStatus.set('loading');
    this.partiesService
      .listSummaries()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (rows: PartySummary[]) => {
          this.parties.set(rows);
          this.listStatus.set('ready');
        },
        error: () => this.listStatus.set('error')
      }
    );
  }

  private sumByCurrency(side: (row: PartySummary) => PartyCurrencyBalance[]): PartyCurrencyBalance[] {
    const totals: Map<CurrencyCode, number> = new Map();
    for(const party of this.parties()) {
      for(const balance of side(party)) {
        totals.set(balance.currencyCode, (totals.get(balance.currencyCode) ?? 0) + balance.balanceMinorUnits);
      }
    }
    return Array.from(totals.entries()).map(([currencyCode, balanceMinorUnits]) => ({
      currencyCode,
      balanceMinorUnits: fromMinorUnits(balanceMinorUnits)
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
