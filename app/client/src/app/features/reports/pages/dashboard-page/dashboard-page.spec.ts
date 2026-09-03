import { provideZonelessChangeDetection } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Observable, of, throwError } from 'rxjs';
import { AppError } from '../../../../core/types/app-error';
import { Money } from '../../../../core/types/money';
import { ReportsService } from '../../reports-service';
import { CardDueRow } from '../../types/card-due-row';
import { MonthlyExpenseRow } from '../../types/monthly-expense-row';
import { DashboardPage } from './dashboard-page';

/** Structural view of the component's template surface, for assertions in the spec. */
type DashboardView = {
  selectedMonth: () => string;
  monthlyStatus: () => 'loading' | 'ready' | 'error';
  cardDueStatus: () => 'loading' | 'ready' | 'error';
  expensesByCategory: () => Array<{ label: string; totalMinorUnits: number }>;
  accruedByCard: () => Array<{ label: string; totalMinorUnits: number }>;
  futureByCard: () => Array<{ label: string; totalMinorUnits: number }>;
  onMonthChange: (month: string) => void;
};

describe('DashboardPage', () => {
  let fixture: ComponentFixture<DashboardPage>;
  let view: DashboardView;
  let monthlyExpenses: jasmine.Spy<(month?: string) => Observable<MonthlyExpenseRow[]>>;
  let cardDueByMonth: jasmine.Spy<() => Observable<CardDueRow[]>>;

  const money = (value: number): Money => value as Money;

  const monthlyRows: MonthlyExpenseRow[] = [
    { month: '2026-09', category: 'Groceries', amountMinorUnits: money(120000), currencyCode: 'ARS' },
    { month: '2026-09', category: 'Groceries', amountMinorUnits: money(30000), currencyCode: 'ARS' },
    { month: '2026-09', category: 'Transport', amountMinorUnits: money(45000), currencyCode: 'ARS' },
  ];
  const cardDueRows: CardDueRow[] = [
    { bucket: 'Accrued', card: 'Visa', cycleYear: 2026, cycleMonth: 9, amountMinorUnits: money(500000), currencyCode: 'ARS', cardId: 'c1' },
    { bucket: 'Future', card: 'Visa', cycleYear: 2026, cycleMonth: 10, amountMinorUnits: money(500000), currencyCode: 'ARS', cardId: 'c1' },
    { bucket: 'Future', card: 'Amex', cycleYear: 2026, cycleMonth: 10, amountMinorUnits: money(250000), currencyCode: 'ARS', cardId: 'c2' },
  ];

  function setup(): void {
    fixture = TestBed.createComponent(DashboardPage);
    view = fixture.componentInstance as unknown as DashboardView;
  }

  beforeEach(() => {
    monthlyExpenses = jasmine.createSpy('monthlyExpenses').and.returnValue(of(monthlyRows));
    cardDueByMonth = jasmine.createSpy('cardDueByMonth').and.returnValue(of(cardDueRows));

    TestBed.configureTestingModule({
      imports: [DashboardPage],
      providers: [
        provideZonelessChangeDetection(),
        { provide: ReportsService, useValue: { monthlyExpenses, cardDueByMonth } },
      ],
    });
  });

  it('creates', () => {
    setup();
    expect(fixture.componentInstance).toBeTruthy();
  });
  it('starts loading and moves to ready once both feeds resolve', () => {
    setup();
    expect(view.monthlyStatus()).toBe('loading');
    expect(view.cardDueStatus()).toBe('loading');
    fixture.detectChanges();
    expect(view.monthlyStatus()).toBe('ready');
    expect(view.cardDueStatus()).toBe('ready');
  });
  it('sums monthly expenses by category', () => {
    setup();
    fixture.detectChanges();
    expect(view.expensesByCategory()).toEqual([
      { label: 'Groceries', totalMinorUnits: 150000 },
      { label: 'Transport', totalMinorUnits: 45000 },
    ]);
  });
  it('splits card dues into Accrued and Future, grouped by card', () => {
    setup();
    fixture.detectChanges();
    expect(view.accruedByCard()).toEqual([{ label: 'Visa', totalMinorUnits: 500000 }]);
    expect(view.futureByCard()).toEqual([
      { label: 'Visa', totalMinorUnits: 500000 },
      { label: 'Amex', totalMinorUnits: 250000 },
    ]);
  });
  it('refetches only monthly expenses when the month changes', () => {
    setup();
    fixture.detectChanges();
    expect(monthlyExpenses).toHaveBeenCalledTimes(1);
    view.onMonthChange('2026-01');
    expect(view.selectedMonth()).toBe('2026-01');
    expect(monthlyExpenses).toHaveBeenCalledTimes(2);
    expect(monthlyExpenses.calls.mostRecent().args).toEqual(['2026-01']);
    expect(cardDueByMonth).toHaveBeenCalledTimes(1);
  });
  it('shows an error state when the monthly feed fails', () => {
    const appError: AppError = { code: 'Http.ServerError', title: 'Server error', detail: 'boom', status: 500, metadata: {} };
    monthlyExpenses.and.returnValue(throwError(() => appError));
    setup();
    fixture.detectChanges();
    expect(view.monthlyStatus()).toBe('error');
    expect(view.cardDueStatus()).toBe('ready');
  });
});
