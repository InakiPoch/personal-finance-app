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
import { Router, RouterLink } from '@angular/router';
import { Subject, takeUntil } from 'rxjs';
import { formatMoney, fromMinorUnits } from '../../../../core/money/money';
import { CurrencyCode } from '../../../../core/types/currency-code';
import { Money } from '../../../../core/types/money';
import { FinancingService } from '../../../financing/financing-service';
import { CardPurchaseRow } from '../../../financing/types/card-purchase-row';
import { DueThisMonthRow } from '../../../financing/types/due-this-month-row';
import { PartiesService } from '../../../parties/parties-service';
import { Party } from '../../../parties/types/party';
import { SubscriptionsService } from '../../../subscriptions/subscriptions-service';
import { MonthSubscription } from '../../../subscriptions/types/month-subscription';
import { ReportsService } from '../../reports-service';
import { CardDueRow } from '../../types/card-due-row';
import { OwedToYouRow } from '../../types/owed-to-you-row';
import { YouOweRow } from '../../types/you-owe-row';
import { MonthlyExpenseRow } from '../../types/monthly-expense-row';
import { MonthlyIncomeRow } from '../../types/monthly-income-row';

type LoadStatus = 'loading' | 'ready' | 'error';
type FlowSide = 'out' | 'in';
type Grouping = { label: string; currencyCode: CurrencyCode; totalMinorUnits: Money };
type CurrencyTotal = { currencyCode: CurrencyCode; totalMinorUnits: Money };
type DueByCurrency = {
  currencyCode: CurrencyCode;
  total: Money;
  cards: Money;
  creditors: Money;
  sources: DueThisMonthRow[];
};
type CardCycle = {
  card: string;
  cardId: string | null;
  currencyCode: CurrencyCode;
  accrued: Money;
  future: Money;
  total: Money;
  barTotal: Money;
};

function localTodayKey(): string {
  const now: Date = new Date();
  return `${currentMonthKey()}-${String(now.getDate()).padStart(2, '0')}`;
}

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
  protected readonly flowSide: WritableSignal<FlowSide> = signal<FlowSide>('out');
  protected readonly monthlyStatus: WritableSignal<LoadStatus> = signal<LoadStatus>('loading');
  protected readonly incomeStatus: WritableSignal<LoadStatus> = signal<LoadStatus>('loading');
  protected readonly cardDueStatus: WritableSignal<LoadStatus> = signal<LoadStatus>('loading');
  protected readonly dueStatus: WritableSignal<LoadStatus> = signal<LoadStatus>('loading');
  protected readonly owedStatus: WritableSignal<LoadStatus> = signal<LoadStatus>('loading');
  protected readonly owedRows: WritableSignal<OwedToYouRow[]> = signal<OwedToYouRow[]>([]);
  protected readonly youOweStatus: WritableSignal<LoadStatus> = signal<LoadStatus>('loading');
  protected readonly youOweRows: WritableSignal<YouOweRow[]> = signal<YouOweRow[]>([]);
  protected readonly debtPickerOpen: WritableSignal<boolean> = signal<boolean>(false);
  protected readonly debtParties: WritableSignal<Party[]> = signal<Party[]>([]);
  protected readonly debtPartiesStatus: WritableSignal<LoadStatus> = signal<LoadStatus>('loading');
  protected readonly dueExpanded: WritableSignal<boolean> = signal<boolean>(false);
  protected readonly subscriptionsStatus: WritableSignal<LoadStatus> = signal<LoadStatus>('loading');
  protected readonly monthSubscriptions: WritableSignal<MonthSubscription[]> = signal<MonthSubscription[]>([]);
  protected readonly isCurrentMonth: Signal<boolean> = computed(() => this.selectedMonth() === currentMonthKey());
  protected readonly isFutureMonth: Signal<boolean> = computed(() => this.selectedMonth() > currentMonthKey());
  protected readonly pageTitle: Signal<string> = computed(() => {
    if(this.isCurrentMonth()) {
      return 'This month';
    }
    const [year, month]: number[] = this.selectedMonth().split('-').map(Number);
    return new Intl.DateTimeFormat('en-US', { month: 'long', year: 'numeric' }).format(new Date(year, month - 1, 1));
  });

  protected readonly expensesByCategory: Signal<Grouping[]> = computed(() =>
    sumByLabel(
      this.monthlyRows(),
      (row: MonthlyExpenseRow) => row.category,
      (row: MonthlyExpenseRow) => row.currencyCode,
      (row: MonthlyExpenseRow) => row.amountMinorUnits
    ).sort((a: Grouping, b: Grouping) =>
      a.currencyCode.localeCompare(b.currencyCode) || b.totalMinorUnits - a.totalMinorUnits
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

  protected readonly monthlyTotalsByCurrency: Signal<CurrencyTotal[]> = computed(() => {
    const totals: CurrencyTotal[] = sumByCurrency(
      this.monthlyRows(),
      (row: MonthlyExpenseRow) => row.currencyCode,
      (row: MonthlyExpenseRow) => row.amountMinorUnits
    );
    return totals.length > 0 ? totals : [{ currencyCode: 'ARS', totalMinorUnits: fromMinorUnits(0) }];
  });

  protected readonly incomeTotalsByCurrency: Signal<CurrencyTotal[]> = computed(() => {
    const totals: CurrencyTotal[] = sumByCurrency(
      this.incomeRows(),
      (row: MonthlyIncomeRow) => row.currencyCode,
      (row: MonthlyIncomeRow) => row.amountMinorUnits
    );
    return totals.length > 0 ? totals : [{ currencyCode: 'ARS', totalMinorUnits: fromMinorUnits(0) }];
  });
  
  protected readonly dueByCurrency: Signal<DueByCurrency[]> = computed(() => {
    const byCurrency: Map<CurrencyCode, DueThisMonthRow[]> = new Map<CurrencyCode, DueThisMonthRow[]>();
    for(const row of this.dueRows()) {
      byCurrency.set(row.currencyCode, [...(byCurrency.get(row.currencyCode) ?? []), row]);
    }
    const sumKind = (rows: DueThisMonthRow[], kind: DueThisMonthRow['kind']): Money =>
      fromMinorUnits(
        rows.filter((row: DueThisMonthRow) => row.kind === kind).reduce((sum: number, row: DueThisMonthRow) => sum + row.amountMinorUnits, 0)
      );
    return Array.from(byCurrency, ([currencyCode, sources]: [CurrencyCode, DueThisMonthRow[]]) => {
      const cards: Money = sumKind(sources, 'card');
      const creditors: Money = sumKind(sources, 'creditor');
      return { currencyCode, total: fromMinorUnits(cards + creditors), cards, creditors, sources };
    });
  });

  protected readonly maxCategoryAmount: Signal<number> = computed(() =>
    this.expensesByCategory().reduce((max: number, group: Grouping) => Math.max(max, group.totalMinorUnits), 0)
  );

  protected readonly monthCardRows: Signal<CardDueRow[]> = computed(() => {
    const [year, month]: number[] = this.selectedMonth().split('-').map(Number);
    const current: boolean = this.isCurrentMonth();
    const nextYear: number = month === 12 ? year + 1 : year;
    const nextMonth: number = month === 12 ? 1 : month + 1;
    return this.cardDueRows().filter((row: CardDueRow) =>
      row.cycleYear === null
        ? current && row.bucket === 'Accrued'
      : (row.cycleYear === year && row.cycleMonth === month) || (current && row.cycleYear === nextYear && row.cycleMonth === nextMonth)
    );
  });

  protected readonly cycleByCard: Signal<CardCycle[]> = computed(() => {
    const order: string[] = [];
    const cardIdByKey: Map<string, string | null> = new Map<string, string | null>();
    const currencyByKey: Map<string, CurrencyCode> = new Map<string, CurrencyCode>();
    const labelByKey: Map<string, string> = new Map<string, string>();
    const accrued: Map<string, number> = new Map<string, number>();
    const future: Map<string, number> = new Map<string, number>();
    const keyOf = (row: CardDueRow): string => `${row.cardId ?? `label:${row.card}`}|${row.currencyCode}`;

    for(const row of this.monthCardRows()) {
      const key: string = keyOf(row);
      if(!cardIdByKey.has(key)) {
        cardIdByKey.set(key, row.cardId);
        currencyByKey.set(key, row.currencyCode);
      }
      if(row.bucket === 'Accrued' || !labelByKey.has(key)) {
        labelByKey.set(key, row.card.replace(/ Liability$/, ''));
      }
      const totals: Map<string, number> = row.bucket === 'Accrued' ? accrued : future;
      totals.set(key, (totals.get(key) ?? 0) + row.amountMinorUnits);
    }

    for(const row of this.monthCardRows()) {
      if(row.bucket !== 'Accrued') {
        continue;
      }
      const key: string = keyOf(row);
      if(!order.includes(key)) {
        order.push(key);
      }
    }
    for(const row of this.monthCardRows()) {
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
        total: fromMinorUnits(this.isCurrentMonth() ? accruedMinor : accruedMinor + futureMinor),
        barTotal: fromMinorUnits(accruedMinor + futureMinor)
      };
    });
  });

  protected readonly expandedCardId: WritableSignal<string | null> = signal<string | null>(null);
  protected readonly purchasesStatus: WritableSignal<LoadStatus> = signal<LoadStatus>('ready');
  protected readonly expandedPurchases: WritableSignal<CardPurchaseRow[]> = signal<CardPurchaseRow[]>([]);

  private readonly reports: ReportsService = inject(ReportsService);
  private readonly financing: FinancingService = inject(FinancingService);
  private readonly subscriptions: SubscriptionsService = inject(SubscriptionsService);
  private readonly parties: PartiesService = inject(PartiesService);
  private readonly router: Router = inject(Router);
  private readonly monthlyRows: WritableSignal<MonthlyExpenseRow[]> = signal<MonthlyExpenseRow[]>([]);
  private readonly incomeRows: WritableSignal<MonthlyIncomeRow[]> = signal<MonthlyIncomeRow[]>([]);
  private readonly dueRows: WritableSignal<DueThisMonthRow[]> = signal<DueThisMonthRow[]>([]);
  private readonly cardDueRows: WritableSignal<CardDueRow[]> = signal<CardDueRow[]>([]);
  private readonly purchasesByCardId: Map<string, CardPurchaseRow[]> = new Map<string, CardPurchaseRow[]>();
  private readonly destroy$: Subject<void> = new Subject<void>();

  protected onMonthChange(month: string): void {
    if(month === '') {
      return;
    }
    this.selectedMonth.set(month);
    this.expandedCardId.set(null);
    this.expandedPurchases.set([]);
    this.purchasesByCardId.clear();
    this.loadMonthlyExpenses();
    this.loadMonthlyIncomes();
    this.loadDueThisMonth();
    this.loadOwedToYou();
    this.loadYouOwe();
    this.loadSubscriptionsByMonth();
  }

  protected setFlowSide(side: FlowSide): void {
    this.flowSide.set(side);
  }

  protected toggleDebtPicker(): void {
    const open: boolean = !this.debtPickerOpen();
    this.debtPickerOpen.set(open);
    if(!open || this.debtPartiesStatus() === 'ready') {
      return;
    }
    this.debtPartiesStatus.set('loading');
    this.parties
      .list()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (rows: Party[]) => {
          this.debtParties.set(rows);
          this.debtPartiesStatus.set('ready');
        },
        error: () => this.debtPartiesStatus.set('error')
      }
    );
  }

  protected registerDebt(partyId: string): void {
    if(partyId !== '') {
      void this.router.navigate(['/parties', partyId], { fragment: 'debt-forms' });
    }
  }

  protected toggleDueBreakdown(): void {
    this.dueExpanded.update((expanded: boolean) => !expanded);
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
    const month: string = this.selectedMonth();
    const cached: CardPurchaseRow[] | undefined = this.purchasesByCardId.get(cardId);
    if(cached) {
      this.expandedPurchases.set(cached);
      this.purchasesStatus.set('ready');
      return;
    }
    this.purchasesStatus.set('loading');
    this.financing
      .cardPurchases(cardId, month, localTodayKey())
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (rows: CardPurchaseRow[]) => {
          if(month !== this.selectedMonth() || this.expandedCardId() !== cardId) {
            return;
          }
          this.purchasesByCardId.set(cardId, rows);
          this.expandedPurchases.set(rows);
          this.purchasesStatus.set('ready');
        },
        error: () => this.purchasesStatus.set('error')
      }
    );
  }

  private loadMonthlyExpenses(): void {
    const month: string = this.selectedMonth();
    this.monthlyStatus.set('loading');
    this.reports
      .monthlyExpenses(month)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (rows: MonthlyExpenseRow[]) => {
          if(month !== this.selectedMonth()) {
            return;
          }
          this.monthlyRows.set(rows);
          this.monthlyStatus.set('ready');
        },
        error: () => {
          if(month === this.selectedMonth()) {
            this.monthlyStatus.set('error');
          }
        }
      }
    );
  }

  private loadMonthlyIncomes(): void {
    const month: string = this.selectedMonth();
    this.incomeStatus.set('loading');
    this.reports
      .monthlyIncomes(month)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (rows: MonthlyIncomeRow[]) => {
          if(month !== this.selectedMonth()) {
            return;
          }
          this.incomeRows.set(rows);
          this.incomeStatus.set('ready');
        },
        error: () => {
          if(month === this.selectedMonth()) {
            this.incomeStatus.set('error');
          }
        }
      }
    );
  }

  private loadDueThisMonth(): void {
    const month: string = this.selectedMonth();
    this.dueStatus.set('loading');
    this.financing
      .dueThisMonth(month, localTodayKey())
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (rows: DueThisMonthRow[]) => {
          if(month !== this.selectedMonth()) {
            return;
          }
          this.dueRows.set(rows);
          this.dueStatus.set('ready');
        },
        error: () => {
          if(month === this.selectedMonth()) {
            this.dueStatus.set('error');
          }
        }
      }
    );
  }

  private loadOwedToYou(): void {
    const month: string = this.selectedMonth();
    this.owedStatus.set('loading');
    this.reports
      .owedToYou(month, localTodayKey())
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (rows: OwedToYouRow[]) => {
          if(month !== this.selectedMonth()) {
            return;
          }
          this.owedRows.set(rows);
          this.owedStatus.set('ready');
        },
        error: () => {
          if(month === this.selectedMonth()) {
            this.owedStatus.set('error');
          }
        }
      }
    );
  }

  private loadYouOwe(): void {
    const month: string = this.selectedMonth();
    this.youOweStatus.set('loading');
    this.reports
      .youOwe(month, localTodayKey())
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (rows: YouOweRow[]) => {
          if(month !== this.selectedMonth()) {
            return;
          }
          this.youOweRows.set(rows);
          this.youOweStatus.set('ready');
        },
        error: () => {
          if(month === this.selectedMonth()) {
            this.youOweStatus.set('error');
          }
        }
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

  private loadSubscriptionsByMonth(): void {
    const month: string = this.selectedMonth();
    this.subscriptionsStatus.set('loading');
    this.subscriptions
      .listByMonth(month)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (rows: MonthSubscription[]) => {
          if(month !== this.selectedMonth()) {
            return;
          }
          this.monthSubscriptions.set(
            [...rows].sort((a: MonthSubscription, b: MonthSubscription) => Number(b.status === 'overdue') - Number(a.status === 'overdue'))
          );
          this.subscriptionsStatus.set('ready');
        },
        error: () => {
          if(month === this.selectedMonth()) {
            this.subscriptionsStatus.set('error');
          }
        }
      }
    );
  }

  ngOnInit(): void {
    this.loadMonthlyExpenses();
    this.loadMonthlyIncomes();
    this.loadDueThisMonth();
    this.loadOwedToYou();
    this.loadYouOwe();
    this.loadCardDue();
    this.loadSubscriptionsByMonth();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }
}
