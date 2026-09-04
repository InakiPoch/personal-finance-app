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
import { formatArs, fromMinorUnits } from '../../../../core/money/money';
import { Money } from '../../../../core/types/money';
import { ReportsService } from '../../reports-service';
import { CardDueRow } from '../../types/card-due-row';
import { MonthlyExpenseRow } from '../../types/monthly-expense-row';

type LoadStatus = 'loading' | 'ready' | 'error';
type Grouping = { label: string; totalMinorUnits: Money };
type CardCycle = { card: string; accrued: Money; future: Money; total: Money };

function currentMonthKey(): string {
  const now: Date = new Date();
  return `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}`;
}

/** Sum minor-unit amounts by a string label, preserving first-seen order. */
function sumByLabel<T>(rows: T[], labelOf: (row: T) => string, amountOf: (row: T) => number): Grouping[] {
  const totals: Map<string, number> = new Map<string, number>();
  for(const row of rows) {
    const label: string = labelOf(row);
    totals.set(label, (totals.get(label) ?? 0) + amountOf(row));
  }
  return Array.from(totals, ([label, total]: [string, number]) => ({
    label,
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
  protected readonly formatArs: (value: Money) => string = formatArs;

  protected readonly selectedMonth: WritableSignal<string> = signal<string>(currentMonthKey());
  protected readonly monthlyStatus: WritableSignal<LoadStatus> = signal<LoadStatus>('loading');
  protected readonly cardDueStatus: WritableSignal<LoadStatus> = signal<LoadStatus>('loading');

  protected readonly expensesByCategory: Signal<Grouping[]> = computed(() =>
    sumByLabel(this.monthlyRows(), (row: MonthlyExpenseRow) => row.category, (row: MonthlyExpenseRow) => row.amountMinorUnits)
  );
  protected readonly accruedByCard: Signal<Grouping[]> = computed(() =>
    sumByLabel(this.cardDueRows().filter((row: CardDueRow) => row.bucket === 'Accrued'), (row: CardDueRow) => row.card, (row: CardDueRow) => row.amountMinorUnits)
  );
  protected readonly futureByCard: Signal<Grouping[]> = computed(() =>
    sumByLabel(this.cardDueRows().filter((row: CardDueRow) => row.bucket === 'Future'), (row: CardDueRow) => row.card, (row: CardDueRow) => row.amountMinorUnits)
  );

  protected readonly monthlyTotal: Signal<Money> = computed(() =>
    fromMinorUnits(this.expensesByCategory().reduce((sum: number, group: Grouping) => sum + group.totalMinorUnits, 0))
  );

  protected readonly maxCategoryAmount: Signal<number> = computed(() =>
    this.expensesByCategory().reduce((max: number, group: Grouping) => Math.max(max, group.totalMinorUnits), 0)
  );

  protected readonly cycleByCard: Signal<CardCycle[]> = computed(() => {
    const order: string[] = [];
    const accrued: Map<string, number> = new Map<string, number>();
    const future: Map<string, number> = new Map<string, number>();
    for(const row of this.accruedByCard()) {
      accrued.set(row.label, row.totalMinorUnits);
      order.push(row.label);
    }
    for(const row of this.futureByCard()) {
      future.set(row.label, row.totalMinorUnits);
      if(!order.includes(row.label)) {
        order.push(row.label);
      }
    }
    return order.map((card: string) => {
      const accruedMinor: number = accrued.get(card) ?? 0;
      const futureMinor: number = future.get(card) ?? 0;
      return {
        card,
        accrued: fromMinorUnits(accruedMinor),
        future: fromMinorUnits(futureMinor),
        total: fromMinorUnits(accruedMinor + futureMinor)
      };
    });
  });

  private readonly reports: ReportsService = inject(ReportsService);
  private readonly monthlyRows: WritableSignal<MonthlyExpenseRow[]> = signal<MonthlyExpenseRow[]>([]);
  private readonly cardDueRows: WritableSignal<CardDueRow[]> = signal<CardDueRow[]>([]);
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
        error: () => this.monthlyStatus.set('error'),
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
        error: () => this.cardDueStatus.set('error'),
      }
    );
  }

  ngOnInit(): void {
    this.loadMonthlyExpenses();
    this.loadCardDue();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }
}
