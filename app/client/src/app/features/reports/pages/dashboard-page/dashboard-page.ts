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
import { formatMoney, fromMinorUnits } from '../../../../core/money/money';
import { CurrencyCode } from '../../../../core/types/currency-code';
import { Money } from '../../../../core/types/money';
import { FinancingService } from '../../../financing/financing-service';
import { CardPurchaseRow } from '../../../financing/types/card-purchase-row';
import { SubscriptionsService } from '../../../subscriptions/subscriptions-service';
import { ActiveSubscription } from '../../../subscriptions/types/active-subscription';
import { ReportsService } from '../../reports-service';
import { CardDueRow } from '../../types/card-due-row';
import { MonthlyExpenseRow } from '../../types/monthly-expense-row';

type LoadStatus = 'loading' | 'ready' | 'error';
type Grouping = { label: string; currencyCode: CurrencyCode; totalMinorUnits: Money };
type CurrencyTotal = { currencyCode: CurrencyCode; totalMinorUnits: Money };
type CardCycle = {
  card: string;
  cardId: string | null;
  currencyCode: CurrencyCode;
  accrued: Money;
  future: Money;
  total: Money;
};

function currentMonthKey(): string {
  const now: Date = new Date();
  return `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}`;
}

/** Sum minor-unit amounts by label + currency, preserving first-seen order. */
function sumByLabel<T>(
  rows: T[],
  labelOf: (row: T) => string,
  currencyOf: (row: T) => CurrencyCode,
  amountOf: (row: T) => number
): Grouping[] {
  const totals: Map<string, { label: string; currencyCode: CurrencyCode; total: number }> = new Map();
  for(const row of rows) {
    const label: string = labelOf(row);
    const currencyCode: CurrencyCode = currencyOf(row);
    const key: string = `${label}|${currencyCode}`;
    const existing = totals.get(key);
    totals.set(key, { label, currencyCode, total: (existing?.total ?? 0) + amountOf(row) });
  }
  return Array.from(totals.values(), ({ label, currencyCode, total }) => ({
    label,
    currencyCode,
    totalMinorUnits: fromMinorUnits(total)
  }));
}

/** Sum minor-unit amounts by currency alone, preserving first-seen order. */
function sumByCurrency<T>(rows: T[], currencyOf: (row: T) => CurrencyCode, amountOf: (row: T) => number): CurrencyTotal[] {
  const totals: Map<CurrencyCode, number> = new Map<CurrencyCode, number>();
  for(const row of rows) {
    const currencyCode: CurrencyCode = currencyOf(row);
    totals.set(currencyCode, (totals.get(currencyCode) ?? 0) + amountOf(row));
  }
  return Array.from(totals, ([currencyCode, total]: [CurrencyCode, number]) => ({
    currencyCode,
    totalMinorUnits: fromMinorUnits(total)
  }));
}

@Component({
  selector: 'app-dashboard-page',
  imports: [RouterLink],
  templateUrl: './dashboard-page.html',
  styleUrl: './dashboard-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class DashboardPage implements OnInit, OnDestroy {
  protected readonly formatMoney: (value: Money, code: CurrencyCode) => string = formatMoney;

  protected readonly selectedMonth: WritableSignal<string> = signal<string>(currentMonthKey());
  protected readonly monthlyStatus: WritableSignal<LoadStatus> = signal<LoadStatus>('loading');
  protected readonly cardDueStatus: WritableSignal<LoadStatus> = signal<LoadStatus>('loading');
  protected readonly subscriptionsStatus: WritableSignal<LoadStatus> = signal<LoadStatus>('loading');
  protected readonly activeSubscriptions: WritableSignal<ActiveSubscription[]> = signal<ActiveSubscription[]>([]);

  protected readonly expensesByCategory: Signal<Grouping[]> = computed(() =>
    sumByLabel(
      this.monthlyRows(),
      (row: MonthlyExpenseRow) => row.category,
      (row: MonthlyExpenseRow) => row.currencyCode,
      (row: MonthlyExpenseRow) => row.amountMinorUnits
    )
  );
  protected readonly accruedByCard: Signal<Grouping[]> = computed(() =>
    sumByLabel(
      this.cardDueRows().filter((row: CardDueRow) => row.bucket === 'Accrued'),
      (row: CardDueRow) => row.card,
      (row: CardDueRow) => row.currencyCode,
      (row: CardDueRow) => row.amountMinorUnits
    )
  );
  protected readonly futureByCard: Signal<Grouping[]> = computed(() =>
    sumByLabel(
      this.cardDueRows().filter((row: CardDueRow) => row.bucket === 'Future'),
      (row: CardDueRow) => row.card,
      (row: CardDueRow) => row.currencyCode,
      (row: CardDueRow) => row.amountMinorUnits
    )
  );

  protected readonly monthlyTotalsByCurrency: Signal<CurrencyTotal[]> = computed(() =>
    sumByCurrency(this.monthlyRows(), (row: MonthlyExpenseRow) => row.currencyCode, (row: MonthlyExpenseRow) => row.amountMinorUnits)
  );

  protected readonly maxCategoryAmount: Signal<number> = computed(() =>
    this.expensesByCategory().reduce((max: number, group: Grouping) => Math.max(max, group.totalMinorUnits), 0)
  );

  protected readonly cycleByCard: Signal<CardCycle[]> = computed(() => {
    const order: string[] = [];
    const cardIdByKey: Map<string, string | null> = new Map<string, string | null>();
    const currencyByKey: Map<string, CurrencyCode> = new Map<string, CurrencyCode>();
    const labelByKey: Map<string, string> = new Map<string, string>();
    const accrued: Map<string, number> = new Map<string, number>();
    const future: Map<string, number> = new Map<string, number>();
    const keyOf = (row: CardDueRow): string => `${row.cardId ?? `label:${row.card}`}|${row.currencyCode}`;

    for(const row of this.cardDueRows()) {
      const key: string = keyOf(row);
      if(!cardIdByKey.has(key)) {
        cardIdByKey.set(key, row.cardId);
        currencyByKey.set(key, row.currencyCode);
      }
      if(row.bucket === 'Accrued' || !labelByKey.has(key)) {
        labelByKey.set(key, row.card);
      }
      const totals: Map<string, number> = row.bucket === 'Accrued' ? accrued : future;
      totals.set(key, (totals.get(key) ?? 0) + row.amountMinorUnits);
    }

    for(const row of this.cardDueRows()) {
      if(row.bucket !== 'Accrued') {
        continue;
      }
      const key: string = keyOf(row);
      if(!order.includes(key)) {
        order.push(key);
      }
    }
    for(const row of this.cardDueRows()) {
      if(row.bucket !== 'Future') {
        continue;
      }
      const key: string = keyOf(row);
      if(!order.includes(key)) {
        order.push(key);
      }
    }

    return order.map((key: string) => {
      const accruedMinor: number = accrued.get(key) ?? 0;
      const futureMinor: number = future.get(key) ?? 0;
      return {
        card: labelByKey.get(key) ?? key,
        cardId: cardIdByKey.get(key) ?? null,
        currencyCode: currencyByKey.get(key) ?? 'ARS',
        accrued: fromMinorUnits(accruedMinor),
        future: fromMinorUnits(futureMinor),
        total: fromMinorUnits(accruedMinor + futureMinor)
      };
    });
  });

  protected readonly expandedCardId: WritableSignal<string | null> = signal<string | null>(null);
  protected readonly purchasesStatus: WritableSignal<LoadStatus> = signal<LoadStatus>('ready');
  protected readonly expandedPurchases: WritableSignal<CardPurchaseRow[]> = signal<CardPurchaseRow[]>([]);

  private readonly reports: ReportsService = inject(ReportsService);
  private readonly financing: FinancingService = inject(FinancingService);
  private readonly subscriptions: SubscriptionsService = inject(SubscriptionsService);
  private readonly monthlyRows: WritableSignal<MonthlyExpenseRow[]> = signal<MonthlyExpenseRow[]>([]);
  private readonly cardDueRows: WritableSignal<CardDueRow[]> = signal<CardDueRow[]>([]);
  private readonly purchasesByCardId: Map<string, CardPurchaseRow[]> = new Map<string, CardPurchaseRow[]>();
  private readonly destroy$: Subject<void> = new Subject<void>();

  protected onMonthChange(month: string): void {
    this.selectedMonth.set(month);
    this.loadMonthlyExpenses();
  }

  /** Width (%) of the proportion rule behind a category row, relative to the largest. */
  protected barWidth(amount: Money): number {
    const max: number = this.maxCategoryAmount();
    return max === 0 ? 0 : Math.round((amount / max) * 100);
  }

  /** Share (%) one segment takes of a card's cycle bar. */
  protected segmentWidth(part: Money, total: Money): number {
    return total === 0 ? 0 : (part / total) * 100;
  }

  protected toggleCardPurchases(cardId: string | null): void {
    if(cardId === null) {
      return;
    }
    if(this.expandedCardId() === cardId) {
      this.expandedCardId.set(null);
      this.expandedPurchases.set([]);
      return;
    }
    this.expandedCardId.set(cardId);
    const cached: CardPurchaseRow[] | undefined = this.purchasesByCardId.get(cardId);
    if(cached) {
      this.expandedPurchases.set(cached);
      this.purchasesStatus.set('ready');
      return;
    }
    this.purchasesStatus.set('loading');
    this.financing
      .cardPurchases(cardId)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (rows: CardPurchaseRow[]) => {
          this.purchasesByCardId.set(cardId, rows);
          this.expandedPurchases.set(rows);
          this.purchasesStatus.set('ready');
        },
        error: () => this.purchasesStatus.set('error')
      }
    );
  }

  private loadMonthlyExpenses(): void {
    this.monthlyStatus.set('loading');
    this.reports
      .monthlyExpenses(this.selectedMonth())
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (rows: MonthlyExpenseRow[]) => {
          this.monthlyRows.set(rows);
          this.monthlyStatus.set('ready');
        },
        error: () => this.monthlyStatus.set('error')
      }
    );
  }

  private loadCardDue(): void {
    this.cardDueStatus.set('loading');
    this.reports
      .cardDueByMonth()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (rows: CardDueRow[]) => {
          this.cardDueRows.set(rows);
          this.cardDueStatus.set('ready');
        },
        error: () => this.cardDueStatus.set('error')
      }
    );
  }

  private loadActiveSubscriptions(): void {
    this.subscriptionsStatus.set('loading');
    this.subscriptions
      .listActive()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (rows: ActiveSubscription[]) => {
          this.activeSubscriptions.set(rows);
          this.subscriptionsStatus.set('ready');
        },
        error: () => this.subscriptionsStatus.set('error')
      }
    );
  }

  ngOnInit(): void {
    this.loadMonthlyExpenses();
    this.loadCardDue();
    this.loadActiveSubscriptions();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }
}
