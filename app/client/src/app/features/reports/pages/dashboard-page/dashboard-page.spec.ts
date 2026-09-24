import { provideZonelessChangeDetection } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Observable, of, throwError } from 'rxjs';
import { formatArs } from '../../../../core/money/money';
import { AppError } from '../../../../core/types/app-error';
import { Money } from '../../../../core/types/money';
import { FinancingService } from '../../../financing/financing-service';
import { CardPurchaseRow } from '../../../financing/types/card-purchase-row';
import { SubscriptionsService } from '../../../subscriptions/subscriptions-service';
import { ActiveSubscription } from '../../../subscriptions/types/active-subscription';
import { ReportsService } from '../../reports-service';
import { CardDueRow } from '../../types/card-due-row';
import { MonthlyExpenseRow } from '../../types/monthly-expense-row';
import { DashboardPage } from './dashboard-page';

type DashboardView = {
  selectedMonth: () => string;
  monthlyStatus: () => 'loading' | 'ready' | 'error';
  cardDueStatus: () => 'loading' | 'ready' | 'error';
  subscriptionsStatus: () => 'loading' | 'ready' | 'error';
  activeSubscriptions: () => ActiveSubscription[];
  expensesByCategory: () => Array<{ label: string; currencyCode: string; totalMinorUnits: number }>;
  accruedByCard: () => Array<{ label: string; currencyCode: string; totalMinorUnits: number }>;
  futureByCard: () => Array<{ label: string; currencyCode: string; totalMinorUnits: number }>;
  monthlyTotalsByCurrency: () => Array<{ currencyCode: string; totalMinorUnits: number }>;
  cycleByCard: () => Array<{ card: string; cardId: string | null; accrued: number; future: number; total: number }>;
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
  let listActive: jasmine.Spy<() => Observable<ActiveSubscription[]>>;

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
    { planId: 'p1', description: 'New laptop', totalMinorUnits: money(300000), installmentCount: 6, outstandingCount: 3, purchaseDate: '2026-06-01' }
  ];
  const activeSubscriptions: ActiveSubscription[] = [
    { subscriptionId: 's1', name: 'Netflix', amountMinorUnits: money(150000), category: 'Streaming', frequency: 'monthly', anchorDay: 5, nextDueDate: '2026-09-05', status: 'paid' },
    { subscriptionId: 's2', name: 'Spotify', amountMinorUnits: money(80000), category: 'Streaming', frequency: 'monthly', anchorDay: 1, nextDueDate: '2026-09-01', status: 'overdue' },
    { subscriptionId: 's3', name: 'iCloud', amountMinorUnits: money(20000), category: 'Storage', frequency: 'monthly', anchorDay: 28, nextDueDate: '2026-09-28', status: 'upcoming' }
  ];

  function setup(): void {
    fixture = TestBed.createComponent(DashboardPage);
    view = fixture.componentInstance as unknown as DashboardView;
  }

  beforeEach(() => {
    monthlyExpenses = jasmine.createSpy('monthlyExpenses').and.returnValue(of(monthlyRows));
    cardDueByMonth = jasmine.createSpy('cardDueByMonth').and.returnValue(of(cardDueRows));
    cardPurchases = jasmine.createSpy('cardPurchases').and.returnValue(of(purchaseRows));
    listActive = jasmine.createSpy('listActive').and.returnValue(of(activeSubscriptions));

    TestBed.configureTestingModule({
      imports: [DashboardPage],
      providers: [
        provideZonelessChangeDetection(),
        provideRouter([]),
        { provide: ReportsService, useValue: { monthlyExpenses, cardDueByMonth } },
        { provide: FinancingService, useValue: { cardPurchases } },
        { provide: SubscriptionsService, useValue: { listActive } }
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
      { label: 'Groceries', currencyCode: 'ARS', totalMinorUnits: 150000 },
      { label: 'Transport', currencyCode: 'ARS', totalMinorUnits: 45000 }
    ]);
  });
  it('splits card dues into Accrued and Future, grouped by card', () => {
    setup();
    fixture.detectChanges();
    expect(view.accruedByCard()).toEqual([{ label: 'Visa', currencyCode: 'ARS', totalMinorUnits: 500000 }]);
    expect(view.futureByCard()).toEqual([
      { label: 'Visa', currencyCode: 'ARS', totalMinorUnits: 500000 },
      { label: 'Amex', currencyCode: 'ARS', totalMinorUnits: 250000 }
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
    expect(text).toContain('3 of 6 installments paid');
  });
  it('collapses one card into a single row when Accrued and Future carry different labels', () => {
    cardDueByMonth.and.returnValue(of([
      { bucket: 'Accrued', card: 'Visa', cycleYear: null, cycleMonth: null, amountMinorUnits: money(500000), currencyCode: 'ARS', cardId: 'c1' },
      { bucket: 'Future', card: '9F3A0B7C-GUID', cycleYear: 2026, cycleMonth: 10, amountMinorUnits: money(500000), currencyCode: 'ARS', cardId: 'c1' }
    ]));
    setup();
    fixture.detectChanges();
    const cards = view.cycleByCard().filter((card) => card.cardId === 'c1');
    expect(cards.length).toBe(1);
    expect(cards[0].card).toBe('Visa');
    expect(cards[0].accrued).toBe(500000);
    expect(cards[0].future).toBe(500000);
    view.toggleCardPurchases('c1');
    fixture.detectChanges();
    expect(cardPurchases).toHaveBeenCalledOnceWith('c1');
    const details = (fixture.nativeElement as HTMLElement).querySelectorAll('[id^="card-purchases-"]');
    expect(details.length).toBe(1);
  });
  it('labels a future-only card with its name, not a GUID', () => {
    cardDueByMonth.and.returnValue(of([
      { bucket: 'Future', card: 'Naranja', cycleYear: 2026, cycleMonth: 11, amountMinorUnits: money(90000), currencyCode: 'ARS', cardId: 'c9' }
    ]));
    setup();
    fixture.detectChanges();
    const card = view.cycleByCard().find((row) => row.cardId === 'c9');
    expect(card?.card).toBe('Naranja');
    expect(card?.future).toBe(90000);
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
  it('renders one row per active subscription with its badge, renewal date, and flat cost', () => {
    setup();
    fixture.detectChanges();
    expect(view.activeSubscriptions()).toEqual(activeSubscriptions);
    const text: string = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Netflix');
    expect(text).toContain('2026-09-05');
    expect(text).toContain(formatArs(money(150000)));
    expect(text).toContain('Paid');
    expect(text).toContain('Spotify');
    expect(text).toContain('Overdue');
    expect(text).toContain('iCloud');
    expect(text).toContain('Upcoming');
  });
  it('keeps the Out of pocket total independent of an overdue subscription', () => {
    setup();
    fixture.detectChanges();
    const text: string = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain(formatArs(money(195000)));
    expect(view.monthlyStatus()).toBe('ready');
  });
  it('keeps mixed ARS and USD monthly expenses as two separated totals, never blended', () => {
    monthlyExpenses.and.returnValue(of([
      { month: '2026-09', category: 'Groceries', amountMinorUnits: money(120000), currencyCode: 'ARS' },
      { month: '2026-09', category: 'Software', amountMinorUnits: money(5000), currencyCode: 'USD' }
    ]));
    setup();
    fixture.detectChanges();
    const totals = view.monthlyTotalsByCurrency();
    expect(totals.length).toBe(2);
    const ars = totals.find((total) => total.currencyCode === 'ARS');
    const usd = totals.find((total) => total.currencyCode === 'USD');
    expect(ars?.totalMinorUnits).toBe(120000);
    expect(usd?.totalMinorUnits).toBe(5000);
    const text: string = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain(formatArs(money(120000)));
  });
  it('shows a loading state for subscriptions, then the panel once ready', () => {
    setup();
    expect(view.subscriptionsStatus()).toBe('loading');
    fixture.detectChanges();
    expect(view.subscriptionsStatus()).toBe('ready');
  });
  it('shows an error state when the subscriptions feed fails, without affecting the other panels', () => {
    const appError: AppError = { code: 'Http.ServerError', title: 'Server error', detail: 'boom', status: 500, metadata: {} };
    listActive.and.returnValue(throwError(() => appError));
    setup();
    fixture.detectChanges();
    expect(view.subscriptionsStatus()).toBe('error');
    expect(view.monthlyStatus()).toBe('ready');
    expect(view.cardDueStatus()).toBe('ready');
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Could not read subscriptions');
  });
  it('shows an empty state when there are no active subscriptions', () => {
    listActive.and.returnValue(of([]));
    setup();
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('No subscriptions to track.');
  });
  it('links the subscriptions panel chevron to /subscriptions', () => {
    setup();
    fixture.detectChanges();
    const link: HTMLAnchorElement | null = (fixture.nativeElement as HTMLElement).querySelector('a[href="/subscriptions"]');
    expect(link).toBeTruthy();
  });
});
