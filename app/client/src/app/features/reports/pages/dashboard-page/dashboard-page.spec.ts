import { provideZonelessChangeDetection } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Observable, of, throwError } from 'rxjs';
import { AppError } from '../../../../core/types/app-error';
import { Money } from '../../../../core/types/money';
import { FinancingService } from '../../../financing/financing-service';
import { CardPurchaseRow } from '../../../financing/types/card-purchase-row';
import { ReportsService } from '../../reports-service';
import { CardDueRow } from '../../types/card-due-row';
import { MonthlyExpenseRow } from '../../types/monthly-expense-row';
import { DashboardPage } from './dashboard-page';

type DashboardView = {
  selectedMonth: () => string;
  monthlyStatus: () => 'loading' | 'ready' | 'error';
  cardDueStatus: () => 'loading' | 'ready' | 'error';
  expensesByCategory: () => Array<{ label: string; totalMinorUnits: number }>;
  accruedByCard: () => Array<{ label: string; totalMinorUnits: number }>;
  futureByCard: () => Array<{ label: string; totalMinorUnits: number }>;
  onMonthChange: (month: string) => void;
  expandedCardId: () => string | null;
  purchasesStatus: () => 'loading' | 'ready' | 'error';
  expandedPurchases: () => CardPurchaseRow[];
  toggleCardPurchases: (cardId: string | null) => void;
};

describe('DashboardPage', () => {
  let fixture: ComponentFixture<DashboardPage>;
  let view: DashboardView;
  let monthlyExpenses: jasmine.Spy<(month?: string) => Observable<MonthlyExpenseRow[]>>;
  let cardDueByMonth: jasmine.Spy<() => Observable<CardDueRow[]>>;
  let cardPurchases: jasmine.Spy<(cardId: string) => Observable<CardPurchaseRow[]>>;

  const money = (value: number): Money => value as Money;

  const monthlyRows: MonthlyExpenseRow[] = [
    { month: '2026-09', category: 'Groceries', amountMinorUnits: money(120000), currencyCode: 'ARS' },
    { month: '2026-09', category: 'Groceries', amountMinorUnits: money(30000), currencyCode: 'ARS' },
    { month: '2026-09', category: 'Transport', amountMinorUnits: money(45000), currencyCode: 'ARS' }
  ];
  const cardDueRows: CardDueRow[] = [
    { bucket: 'Accrued', card: 'Visa', cycleYear: 2026, cycleMonth: 9, amountMinorUnits: money(500000), currencyCode: 'ARS', cardId: 'c1' },
    { bucket: 'Future', card: 'Visa', cycleYear: 2026, cycleMonth: 10, amountMinorUnits: money(500000), currencyCode: 'ARS', cardId: 'c1' },
    { bucket: 'Future', card: 'Amex', cycleYear: 2026, cycleMonth: 10, amountMinorUnits: money(250000), currencyCode: 'ARS', cardId: 'c2' }
  ];
  const purchaseRows: CardPurchaseRow[] = [
    { planId: 'p1', description: 'New laptop', totalMinorUnits: money(300000), installmentCount: 6, outstandingCount: 3, purchaseDate: '2026-06-01', isCreditorPayment: false }
  ];

  function setup(): void {
    fixture = TestBed.createComponent(DashboardPage);
    view = fixture.componentInstance as unknown as DashboardView;
  }

  beforeEach(() => {
    monthlyExpenses = jasmine.createSpy('monthlyExpenses').and.returnValue(of(monthlyRows));
    cardDueByMonth = jasmine.createSpy('cardDueByMonth').and.returnValue(of(cardDueRows));
    cardPurchases = jasmine.createSpy('cardPurchases').and.returnValue(of(purchaseRows));

    TestBed.configureTestingModule({
      imports: [DashboardPage],
      providers: [
        provideZonelessChangeDetection(),
        provideRouter([]),
        { provide: ReportsService, useValue: { monthlyExpenses, cardDueByMonth } },
        { provide: FinancingService, useValue: { cardPurchases } }
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
      { label: 'Transport', totalMinorUnits: 45000 }
    ]);
  });
  it('splits card dues into Accrued and Future, grouped by card', () => {
    setup();
    fixture.detectChanges();
    expect(view.accruedByCard()).toEqual([{ label: 'Visa', totalMinorUnits: 500000 }]);
    expect(view.futureByCard()).toEqual([
      { label: 'Visa', totalMinorUnits: 500000 },
      { label: 'Amex', totalMinorUnits: 250000 }
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
  it('expands a card, calling the service and rendering its purchases', () => {
    setup();
    fixture.detectChanges();
    view.toggleCardPurchases('c1');
    fixture.detectChanges();
    expect(cardPurchases).toHaveBeenCalledOnceWith('c1');
    expect(view.expandedCardId()).toBe('c1');
    expect(view.expandedPurchases()).toEqual(purchaseRows);
    const text: string = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('New laptop');
  });
  it('collapses on a second toggle without calling the service again', () => {
    setup();
    fixture.detectChanges();
    view.toggleCardPurchases('c1');
    fixture.detectChanges();
    view.toggleCardPurchases('c1');
    fixture.detectChanges();
    expect(view.expandedCardId()).toBeNull();
    expect(view.expandedPurchases()).toEqual([]);
    expect(cardPurchases).toHaveBeenCalledTimes(1);
  });
  it('ignores a toggle for a card row with no cardId', () => {
    setup();
    fixture.detectChanges();
    view.toggleCardPurchases(null);
    fixture.detectChanges();
    expect(view.expandedCardId()).toBeNull();
    expect(cardPurchases).not.toHaveBeenCalled();
  });
  it('does not render an expand button for a card row without a cardId', () => {
    cardDueByMonth.and.returnValue(of([
      ...cardDueRows,
      { bucket: 'Future', card: 'MercadoPago', cycleYear: 2026, cycleMonth: 10, amountMinorUnits: money(10000), currencyCode: 'ARS', cardId: null }
    ]));
    setup();
    fixture.detectChanges();
    const buttons: HTMLButtonElement[] = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('button[aria-expanded]'));
    expect(buttons.some((button) => (button.textContent ?? '').includes('MercadoPago'))).toBeFalse();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('MercadoPago');
  });
});
