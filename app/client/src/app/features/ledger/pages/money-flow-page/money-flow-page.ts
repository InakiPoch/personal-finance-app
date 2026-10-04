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
import { RouterLink } from '@angular/router';
import { Subject, takeUntil } from 'rxjs';
import { fromMinorUnits } from '../../../../core/money/money';
import { AppError } from '../../../../core/types/app-error';
import { CurrencyCode } from '../../../../core/types/currency-code';
import { ReportsService } from '../../../reports/reports-service';
import { LedgerService } from '../../ledger-service';
import { MoneyFlowRow } from '../../types/money-flow-row';
import { MoneyFlowCurrencyTotal, MoneyFlowTable } from './money-flow-table';

type LoadStatus = 'loading' | 'ready' | 'error';

function currentMonthKey(): string {
  const now: Date = new Date();
  return `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}`;
}

@Component({
  selector: 'app-money-flow-page',
  imports: [RouterLink, MoneyFlowTable],
  templateUrl: './money-flow-page.html',
  styleUrl: './money-flow-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class MoneyFlowPage implements OnInit, OnDestroy {
  protected readonly selectedMonth: WritableSignal<string> = signal<string>(currentMonthKey());
  protected readonly loadStatus: WritableSignal<LoadStatus> = signal<LoadStatus>('loading');
  protected readonly footerTotals: Signal<MoneyFlowCurrencyTotal[]> = computed(() => {
    const totals: Map<CurrencyCode, { income: number; outcome: number }> = new Map();
    for(const row of this.rows()) {
      const existing = totals.get(row.currencyCode) ?? { income: 0, outcome: 0 };
      if(row.kind === 'Income') {
        existing.income += row.amountMinorUnits;
      } else {
        existing.outcome += row.amountMinorUnits;
      }
      totals.set(row.currencyCode, existing);
    }
    return Array.from(totals, ([currencyCode, { income, outcome }]) => ({
      currencyCode,
      income: fromMinorUnits(income),
      outcome: fromMinorUnits(outcome)
    }));
  });

  protected readonly rows: WritableSignal<MoneyFlowRow[]> = signal<MoneyFlowRow[]>([]);
  protected readonly undoingTransactionId: WritableSignal<string | null> = signal<string | null>(null);
  protected readonly undoError: WritableSignal<AppError | null> = signal<AppError | null>(null);

  private readonly reports: ReportsService = inject(ReportsService);
  private readonly ledger: LedgerService = inject(LedgerService);
  private readonly destroy$: Subject<void> = new Subject<void>();

  protected onMonthChange(month: string): void {
    this.selectedMonth.set(month);
    this.loadMoneyFlow();
  }

  protected onUndo(transactionId: string): void {
    if(!window.confirm('Undo this income?')) {
      return;
    }
    this.undoError.set(null);
    this.undoingTransactionId.set(transactionId);
    this.ledger
      .reverse(transactionId)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.undoingTransactionId.set(null);
          this.loadMoneyFlow();
        },
        error: (error: AppError) => {
          this.undoingTransactionId.set(null);
          this.undoError.set(error);
          if(error.status === 409) {
            this.loadMoneyFlow();
          }
        }
      });
  }

  protected undoErrorText(error: AppError): string {
    return error.status === 409
      ? 'That income was already undone — the row will refresh.'
      : 'Could not undo that income — try again in a moment.';
  }

  private loadMoneyFlow(): void {
    this.loadStatus.set('loading');
    this.reports
      .moneyFlow(this.selectedMonth())
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (rows: MoneyFlowRow[]) => {
          this.rows.set(rows);
          this.loadStatus.set('ready');
        },
        error: () => this.loadStatus.set('error')
      }
    );
  }

  ngOnInit(): void {
    this.loadMoneyFlow();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }
}
