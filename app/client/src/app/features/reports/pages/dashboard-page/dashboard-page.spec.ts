import { provideZonelessChangeDetection } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Observable, Subject, of, throwError } from 'rxjs';
import { formatArs, formatMoney } from '../../../../core/money/money';
import { AppError } from '../../../../core/types/app-error';
import { Money } from '../../../../core/types/money';
import { FinancingService } from '../../../financing/financing-service';
import { CardPurchaseRow } from '../../../financing/types/card-purchase-row';
import { DueThisMonthRow } from '../../../financing/types/due-this-month-row';
import { SubscriptionsService } from '../../../subscriptions/subscriptions-service';
import { MonthSubscription } from '../../../subscriptions/types/month-subscription';
import { ReportsService } from '../../reports-service';
import { CardDueRow } from '../../types/card-due-row';
import { MonthlyExpenseRow } from '../../types/monthly-expense-row';
import { MonthlyIncomeRow } from '../../types/monthly-income-row';
import { DashboardPage } from './dashboard-page';

type DashboardView = {
  selectedMonth: () => string;
  flowSide: () => 'out' | 'in';
  monthlyStatus: () => 'loading' | 'ready' | 'error';
  incomeStatus: () => 'loading' | 'ready' | 'error';
  cardDueStatus: () => 'loading' | 'ready' | 'error';
  dueStatus: () => 'loading' | 'ready' | 'error';
  dueExpanded: () => boolean;
  subscriptionsStatus: () => 'loading' | 'ready' | 'error';
  monthSubscriptions: () => MonthSubscription[];
  expensesByCategory: () => Array<{ label: string; currencyCode: string; totalMinorUnits: number }>;
  accruedByCard: () => Array<{ label: string; currencyCode: string; totalMinorUnits: number }>;
  futureByCard: () => Array<{ label: string; currencyCode: string; totalMinorUnits: number }>;
  monthlyTotalsByCurrency: () => Array<{ currencyCode: string; totalMinorUnits: number }>;
  incomeTotalsByCurrency: () => Array<{ currencyCode: string; totalMinorUnits: number }>;
  cycleByCard: () => Array<{ card: string; cardId: string | null; currencyCode: string; accrued: number; future: number; total: number }>;
  onMonthChange: (month: string) => void;
  setFlowSide: (side: 'out' | 'in') => void;
  expandedCardId: () => string | null;
  purchasesStatus: () => 'loading' | 'ready' | 'error';
  expandedPurchases: () => CardPurchaseRow[];
  toggleCardPurchases: (cardId: string | null) => void;
  toggleDueBreakdown: () => void;
};

/** Month key relative to today, so specs never rot. */
function localToday(): string {
  const now: Date = new Date();
  return `${monthKey(0)}-${String(now.getDate()).padStart(2, '0')}`;
}

function monthKey(offset: number): string {
  const date: Date = new Date();
  const shifted: Date = new Date(date.getFullYear(), date.getMonth() + offset, 1);
  return `${shifted.getFullYear()}-${String(shifted.getMonth() + 1).padStart(2, '0')}`;
}

/** Card-due cycle fields for the month `offset` months from today. */
function cyc(offset: number): { cycleYear: number; cycleMonth: number } {
  const [cycleYear, cycleMonth]: number[] = monthKey(offset).split('-').map(Number);
  return { cycleYear, cycleMonth };
}

describe('DashboardPage', () => {
  let fixture: ComponentFixture<DashboardPage>;
  let view: DashboardView;
  let monthlyExpenses: jasmine.Spy<(month?: string) => Observable<MonthlyExpenseRow[]>>;
  let monthlyIncomes: jasmine.Spy<(month?: string) => Observable<MonthlyIncomeRow[]>>;
  let cardDueByMonth: jasmine.Spy<() => Observable<CardDueRow[]>>;
  let cardPurchases: jasmine.Spy<(cardId: string) => Observable<CardPurchaseRow[]>>;
  let dueThisMonth: jasmine.Spy<(month?: string, today?: string) => Observable<DueThisMonthRow[]>>;
  let listByMonth: jasmine.Spy<(month: string) => Observable<MonthSubscription[]>>;

  const money = (value: number): Money => value as Money;

  const monthlyRows: MonthlyExpenseRow[] = [
    { month: '2026-09', category: 'Groceries', amountMinorUnits: money(120000), currencyCode: 'ARS' },
    { month: '2026-09', category: 'Groceries', amountMinorUnits: money(30000), currencyCode: 'ARS' },
    { month: '2026-09', category: 'Transport', amountMinorUnits: money(45000), currencyCode: 'ARS' }
  ];
  const cardDueRows: CardDueRow[] = [
    { bucket: 'Accrued', card: 'Visa', ...cyc(0), amountMinorUnits: money(500000), currencyCode: 'ARS', cardId: 'c1' },
    { bucket: 'Future', card: 'Visa', ...cyc(0), amountMinorUnits: money(500000), currencyCode: 'ARS', cardId: 'c1' },
    { bucket: 'Future', card: 'Amex', ...cyc(0), amountMinorUnits: money(250000), currencyCode: 'ARS', cardId: 'c2' }
  ];
  const incomeRows: MonthlyIncomeRow[] = [
    { month: '2026-09', amountMinorUnits: money(500000), currencyCode: 'ARS' }
  ];
  const purchaseRows: CardPurchaseRow[] = [
    { planId: 'p1', description: 'New laptop', totalMinorUnits: money(300000), installmentCount: 6, outstandingCount: 3, purchaseDate: '2026-06-01' }
  ];
  const monthSubs: MonthSubscription[] = [
    { subscriptionId: 's1', name: 'Netflix', amountMinorUnits: money(150000), category: 'Streaming', frequency: 'monthly', anchorDay: 5, nextDueDate: '2026-09-05', dueDate: '2026-09-05', status: 'paid', currencyCode: 'ARS' },
    { subscriptionId: 's2', name: 'Spotify', amountMinorUnits: money(80000), category: 'Streaming', frequency: 'monthly', anchorDay: 1, nextDueDate: '2026-09-01', dueDate: '2026-09-01', status: 'overdue', currencyCode: 'ARS' },
    { subscriptionId: 's3', name: 'iCloud', amountMinorUnits: money(20000), category: 'Storage', frequency: 'monthly', anchorDay: 28, nextDueDate: '2026-09-28', dueDate: '2026-09-28', status: 'upcoming', currencyCode: 'ARS' }
  ];

  const dueRows: DueThisMonthRow[] = [
    { kind: 'card', sourceId: 'c1', sourceName: 'Visa', currencyCode: 'ARS', amountMinorUnits: money(500000) },
    { kind: 'creditor', sourceId: 'cr1', sourceName: 'Juan', currencyCode: 'ARS', amountMinorUnits: money(100000) },
    { kind: 'card', sourceId: 'c2', sourceName: 'Amex', currencyCode: 'USD', amountMinorUnits: money(5000) },
    { kind: 'creditor', sourceId: 'cr2', sourceName: 'Maria', currencyCode: 'USD', amountMinorUnits: money(2000) }
  ];

  const pageEl = (): HTMLElement => fixture.nativeElement as HTMLElement;
  const dueCard = (): HTMLElement => pageEl().querySelector('section[aria-labelledby="due-label"]') as HTMLElement;

  const h1 = (): string => (pageEl().querySelector('h1')?.textContent ?? '').trim();
  const dueHeading = (): string => (pageEl().querySelector('#due-label')?.textContent ?? '').trim();
  const subsCard = (): HTMLElement => pageEl().querySelector('section[aria-labelledby="subscriptions-label"]') as HTMLElement;
  const subNames = (): string[] => Array.from(subsCard().querySelectorAll('li .text-ink')).map((el) => (el.textContent ?? '').trim()).filter((t) => ['Netflix', 'Spotify', 'iCloud', 'A', 'B', 'C', 'D'].includes(t));

  /** Picks a month through the real month input, like a user would. */
  function setMonth(month: string): void {
    const input: HTMLInputElement = pageEl().querySelector('nav[aria-label="Quick actions"] input[type="month"]') as HTMLInputElement;
    input.value = month;
    input.dispatchEvent(new Event('change'));
    fixture.detectChanges();
  }

  /** One controllable Subject per month, so specs decide when each response lands. */
  function byMonthSubjects<T>(spy: jasmine.Spy<(month: string) => Observable<T>>): Map<string, Subject<T>> {
    const subjects: Map<string, Subject<T>> = new Map<string, Subject<T>>();
    spy.and.callFake((month: string) => {
      const subject: Subject<T> = new Subject<T>();
      subjects.set(month, subject);
      return subject;
    });
    return subjects;
  }

  function setup(): void {
    fixture = TestBed.createComponent(DashboardPage);
    view = fixture.componentInstance as unknown as DashboardView;
  }

  beforeEach(() => {
    monthlyExpenses = jasmine.createSpy('monthlyExpenses').and.returnValue(of(monthlyRows));
    monthlyIncomes = jasmine.createSpy('monthlyIncomes').and.returnValue(of(incomeRows));
    cardDueByMonth = jasmine.createSpy('cardDueByMonth').and.returnValue(of(cardDueRows));
    cardPurchases = jasmine.createSpy('cardPurchases').and.returnValue(of(purchaseRows));
    dueThisMonth = jasmine.createSpy('dueThisMonth').and.returnValue(of([]));
    listByMonth = jasmine.createSpy('listByMonth').and.returnValue(of([]));

    TestBed.configureTestingModule({
      imports: [DashboardPage],
      providers: [
        provideZonelessChangeDetection(),
        provideRouter([]),
        { provide: ReportsService, useValue: { monthlyExpenses, monthlyIncomes, cardDueByMonth } },
        { provide: FinancingService, useValue: { cardPurchases, dueThisMonth } },
        { provide: SubscriptionsService, useValue: { listByMonth } }
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
  it('refetches monthly expenses, incomes, due and subscriptions, but not card dues, when the month changes', () => {
    setup();
    fixture.detectChanges();
    expect(monthlyExpenses).toHaveBeenCalledTimes(1);
    expect(monthlyIncomes).toHaveBeenCalledTimes(1);
    expect(dueThisMonth).toHaveBeenCalledOnceWith(monthKey(0), localToday());
    expect(listByMonth).toHaveBeenCalledOnceWith(monthKey(0));
    setMonth('2026-01');
    expect(view.selectedMonth()).toBe('2026-01');
    expect(dueThisMonth).toHaveBeenCalledTimes(2);
    expect(dueThisMonth.calls.mostRecent().args).toEqual(['2026-01', localToday()]);
    expect(listByMonth).toHaveBeenCalledTimes(2);
    expect(listByMonth.calls.mostRecent().args).toEqual(['2026-01']);
    expect(monthlyExpenses).toHaveBeenCalledTimes(2);
    expect(monthlyExpenses.calls.mostRecent().args).toEqual(['2026-01']);
    expect(monthlyIncomes).toHaveBeenCalledTimes(2);
    expect(monthlyIncomes.calls.mostRecent().args).toEqual(['2026-01']);
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
      { bucket: 'Future', card: '9F3A0B7C-GUID', ...cyc(0), amountMinorUnits: money(500000), currencyCode: 'ARS', cardId: 'c1' }
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
  it('drops the ledger "Liability" suffix from a charged card label', () => {
    cardDueByMonth.and.returnValue(of([
      { bucket: 'Accrued', card: 'Visa Liability', cycleYear: null, cycleMonth: null, amountMinorUnits: money(500000), currencyCode: 'ARS', cardId: 'c1' }
    ]));
    setup();
    fixture.detectChanges();
    expect(view.cycleByCard().find((row) => row.cardId === 'c1')?.card).toBe('Visa');
  });
  it('labels a future-only card with its name, not a GUID', () => {
    cardDueByMonth.and.returnValue(of([
      { bucket: 'Future', card: 'Naranja', ...cyc(0), amountMinorUnits: money(90000), currencyCode: 'ARS', cardId: 'c9' }
    ]));
    setup();
    fixture.detectChanges();
    const card = view.cycleByCard().find((row) => row.cardId === 'c9');
    expect(card?.card).toBe('Naranja');
    expect(card?.future).toBe(90000);
  });
  it('keeps a card with both ARS and USD accrued rows as two separated cycle entries, never blended', () => {
    cardDueByMonth.and.returnValue(of([
      { bucket: 'Accrued', card: 'Visa', cycleYear: null, cycleMonth: null, amountMinorUnits: money(500000), currencyCode: 'ARS', cardId: 'c1' },
      { bucket: 'Accrued', card: 'Visa', cycleYear: null, cycleMonth: null, amountMinorUnits: money(5000), currencyCode: 'USD', cardId: 'c1' }
    ]));
    setup();
    fixture.detectChanges();
    const cards = view.cycleByCard().filter((card) => card.cardId === 'c1');
    expect(cards.length).toBe(2);
    const ars = cards.find((card) => card.currencyCode === 'ARS');
    const usd = cards.find((card) => card.currencyCode === 'USD');
    expect(ars?.total).toBe(500000);
    expect(usd?.total).toBe(5000);
    const text: string = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain(formatMoney(money(500000), 'ARS'));
    expect(text).toContain(formatMoney(money(5000), 'USD'));
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
      { bucket: 'Future', card: 'MercadoPago', ...cyc(0), amountMinorUnits: money(10000), currencyCode: 'ARS', cardId: null }
    ]));
    setup();
    fixture.detectChanges();
    const buttons: HTMLButtonElement[] = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('button[aria-expanded]'));
    expect(buttons.some((button) => (button.textContent ?? '').includes('MercadoPago'))).toBeFalse();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('MercadoPago');
  });
  it('renders one row per subscription with its badge, due date, and flat cost', () => {
    listByMonth.and.returnValue(of(monthSubs));
    setup();
    fixture.detectChanges();
    expect(view.monthSubscriptions().length).toBe(3);
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
  it('shows a $0 ARS total when no money moved this month', () => {
    monthlyExpenses.and.returnValue(of([]));
    setup();
    fixture.detectChanges();
    const totals = view.monthlyTotalsByCurrency();
    expect(totals).toEqual([{ currencyCode: 'ARS', totalMinorUnits: 0 }]);
    const text: string = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain(formatArs(money(0)));
  });
  it('labels the out-of-pocket total with an explicit USD tag, not just punctuation', () => {
    monthlyExpenses.and.returnValue(of([
      { month: '2026-09', category: 'Software', amountMinorUnits: money(5000), currencyCode: 'USD' }
    ]));
    setup();
    fixture.detectChanges();
    const text: string = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('USD');
  });
  it('labels a USD category row with an explicit currency tag', () => {
    monthlyExpenses.and.returnValue(of([
      { month: '2026-09', category: 'Groceries', amountMinorUnits: money(120000), currencyCode: 'ARS' },
      { month: '2026-09', category: 'Software', amountMinorUnits: money(5000), currencyCode: 'USD' }
    ]));
    setup();
    fixture.detectChanges();
    const rows: NodeListOf<HTMLLIElement> = (fixture.nativeElement as HTMLElement).querySelectorAll('.cat-row');
    const softwareRow: HTMLLIElement | undefined = Array.from(rows).find((row) => (row.textContent ?? '').includes('Software'));
    expect(softwareRow?.textContent ?? '').toContain('USD');
  });
  it('renders a USD subscription through the currency-driven formatter, not the hardcoded ARS one', () => {
    listByMonth.and.returnValue(of([
      { ...monthSubs[0], subscriptionId: 's-usd', name: 'GitHub', amountMinorUnits: money(1200), currencyCode: 'USD' }
    ]));
    setup();
    fixture.detectChanges();
    const text: string = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain(formatMoney(money(1200), 'USD'));
    expect(text).toContain('USD');
  });
  it('shows a loading state for subscriptions, then the panel once ready', () => {
    setup();
    expect(view.subscriptionsStatus()).toBe('loading');
    fixture.detectChanges();
    expect(view.subscriptionsStatus()).toBe('ready');
  });
  it('shows an error state when the subscriptions feed fails, without affecting the other panels', () => {
    const appError: AppError = { code: 'Http.ServerError', title: 'Server error', detail: 'boom', status: 500, metadata: {} };
    listByMonth.and.returnValue(throwError(() => appError));
    setup();
    fixture.detectChanges();
    expect(view.subscriptionsStatus()).toBe('error');
    expect(view.monthlyStatus()).toBe('ready');
    expect(view.cardDueStatus()).toBe('ready');
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Could not read subscriptions');
  });
  it('shows an empty state when there are no subscriptions for the month', () => {
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
  it('defaults to the Out of pocket side, showing its total and category list', () => {
    setup();
    fixture.detectChanges();
    expect(view.flowSide()).toBe('out');
    const text: string = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain(formatArs(money(195000)));
    expect(text).toContain('Groceries');
  });
  it('switches to the Income side on click, showing per-currency totals and hiding the category list', () => {
    monthlyIncomes.and.returnValue(of([
      { month: '2026-09', amountMinorUnits: money(500000), currencyCode: 'ARS' },
      { month: '2026-09', amountMinorUnits: money(20000), currencyCode: 'USD' }
    ]));
    setup();
    fixture.detectChanges();
    view.setFlowSide('in');
    fixture.detectChanges();
    expect(view.flowSide()).toBe('in');
    const totals = view.incomeTotalsByCurrency();
    expect(totals.length).toBe(2);
    const ars = totals.find((total) => total.currencyCode === 'ARS');
    const usd = totals.find((total) => total.currencyCode === 'USD');
    expect(ars?.totalMinorUnits).toBe(500000);
    expect(usd?.totalMinorUnits).toBe(20000);
    const text: string = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain(formatMoney(money(500000), 'ARS'));
    expect(text).toContain(formatMoney(money(20000), 'USD'));
    expect(text).not.toContain('Groceries');
  });
  it('shows a $0 ARS income total when no income was recorded this month', () => {
    monthlyIncomes.and.returnValue(of([]));
    setup();
    fixture.detectChanges();
    view.setFlowSide('in');
    fixture.detectChanges();
    expect(view.incomeTotalsByCurrency()).toEqual([{ currencyCode: 'ARS', totalMinorUnits: 0 }]);
    const text: string = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain(formatArs(money(0)));
  });
  it('links the Record income quick action to /ledger/incomes/new', () => {
    setup();
    fixture.detectChanges();
    const link: HTMLAnchorElement | null = (fixture.nativeElement as HTMLElement).querySelector('a[href="/ledger/incomes/new"]');
    expect(link).toBeTruthy();
  });
  it('renders the quick actions nav before the Due card', () => {
    setup();
    fixture.detectChanges();
    const nav: Element = pageEl().querySelector('nav[aria-label="Quick actions"]') as Element;
    expect(nav).toBeTruthy();
    expect(nav.compareDocumentPosition(dueCard()) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
  });
  it('loads due-this-month on init and sums the Due card per currency, never mixed', () => {
    dueThisMonth.and.returnValue(of(dueRows));
    setup();
    fixture.detectChanges();
    expect(dueThisMonth).toHaveBeenCalledOnceWith(monthKey(0), localToday());
    expect(view.dueStatus()).toBe('ready');
    const text: string = dueCard().textContent ?? '';
    expect(text).toContain(formatMoney(money(600000), 'ARS'));
    expect(text).toContain(formatMoney(money(7000), 'USD'));
    expect(text).not.toContain(formatMoney(money(607000), 'ARS'));
  });
  it('splits Cards and Creditors in the Due card sub-line', () => {
    dueThisMonth.and.returnValue(of(dueRows));
    setup();
    fixture.detectChanges();
    const flat = (value: string): string => value.replace(/\s+/g, ' ');
    const text: string = flat(dueCard().textContent ?? '');
    expect(text).toContain(flat(`Cards ${formatMoney(money(500000), 'ARS')} · Creditors ${formatMoney(money(100000), 'ARS')}`));
    expect(text).toContain(flat(`Cards ${formatMoney(money(5000), 'USD')} · Creditors ${formatMoney(money(2000), 'USD')}`));
  });
  it('expands the breakdown on toggle, listing source names with per-currency amounts', () => {
    dueThisMonth.and.returnValue(of(dueRows));
    setup();
    fixture.detectChanges();
    const button: HTMLButtonElement = dueCard().querySelector('button[aria-controls="due-breakdown"]') as HTMLButtonElement;
    expect(button.getAttribute('aria-expanded')).toBe('false');
    expect(pageEl().querySelector('#due-breakdown')).toBeNull();
    button.click();
    fixture.detectChanges();
    expect(view.dueExpanded()).toBeTrue();
    expect(button.getAttribute('aria-expanded')).toBe('true');
    const items: HTMLElement[] = Array.from(pageEl().querySelectorAll('#due-breakdown li'));
    expect(items.length).toBe(4);
    const itemFor = (name: string): string => items.find((li) => (li.textContent ?? '').includes(name))?.textContent ?? '';
    expect(itemFor('Visa')).toContain(formatMoney(money(500000), 'ARS'));
    expect(itemFor('Juan')).toContain(formatMoney(money(100000), 'ARS'));
    expect(itemFor('Amex')).toContain(formatMoney(money(5000), 'USD'));
    expect(itemFor('Maria')).toContain(formatMoney(money(2000), 'USD'));
    button.click();
    fixture.detectChanges();
    expect(button.getAttribute('aria-expanded')).toBe('false');
    expect(pageEl().querySelector('#due-breakdown')).toBeNull();
  });
  it('shows the Due card empty state when nothing is due', () => {
    setup();
    fixture.detectChanges();
    expect(dueCard().textContent).toContain('Nothing to pay this month.');
    expect(dueCard().querySelector('button[aria-controls="due-breakdown"]')).toBeNull();
  });
  it('shows the Due card error state without affecting other panels', () => {
    const appError: AppError = { code: 'Http.ServerError', title: 'Server error', detail: 'boom', status: 500, metadata: {} };
    dueThisMonth.and.returnValue(throwError(() => appError));
    setup();
    fixture.detectChanges();
    expect(view.dueStatus()).toBe('error');
    expect(dueCard().textContent).toContain("Could not read what's due. Try again.");
    expect(view.monthlyStatus()).toBe('ready');
  });
  it('shows the Due card loading copy before the feed resolves', () => {
    setup();
    expect(view.dueStatus()).toBe('loading');
    fixture.detectChanges();
    expect(view.dueStatus()).toBe('ready');
  });
  it('labels the flow tabs, their group, and the sub-labels', () => {
    setup();
    fixture.detectChanges();
    const group: HTMLElement = pageEl().querySelector('[aria-label="Show money spent or received"]') as HTMLElement;
    expect(group).toBeTruthy();
    const tabs: string[] = Array.from(group.querySelectorAll('button')).map((b) => (b.textContent ?? '').trim());
    expect(tabs).toEqual(['Money Spent', 'Money received']);
    expect(pageEl().textContent).toContain('Your share of what you paid from bank accounts and cash this month.');
    view.setFlowSide('in');
    fixture.detectChanges();
    expect(pageEl().textContent).toContain('Money that came into your bank accounts or cash this month.');
  });
  it('shows the "Where your money went" heading and the empty category copy', () => {
    setup();
    fixture.detectChanges();
    expect(pageEl().textContent).toContain('Where your money went');
    expect(pageEl().textContent).not.toContain('Nothing spent from bank or cash this month.');
  });
  it('shows the empty category copy when nothing was spent', () => {
    monthlyExpenses.and.returnValue(of([]));
    setup();
    fixture.detectChanges();
    expect(pageEl().textContent).toContain('Where your money went');
    expect(pageEl().textContent).toContain('Nothing spent from bank or cash this month.');
  });
  it('labels the card section, its legend, and the Charged / Upcoming sub-line', () => {
    setup();
    fixture.detectChanges();
    const text: string = pageEl().textContent ?? '';
    expect(text).toContain('Card bills by month');
    expect(text).toContain('Charged to the card');
    expect(text).toContain('Upcoming — installments not charged yet');
    expect(text).toContain(`Charged ${formatMoney(money(500000), 'ARS')}`);
    expect(text).toContain(`Upcoming ${formatMoney(money(500000), 'ARS')}`);
    expect(text).toContain('Pay a card bill');
  });
  it('shows the card section loading, error, and empty copy', () => {
    cardDueByMonth.and.returnValue(of([]));
    setup();
    expect(view.cardDueStatus()).toBe('loading');
    fixture.detectChanges();
    expect(pageEl().textContent).toContain('Nothing due on your cards.');
    const appError: AppError = { code: 'Http.ServerError', title: 'Server error', detail: 'boom', status: 500, metadata: {} };
    cardDueByMonth.and.returnValue(throwError(() => appError));
    const second: ComponentFixture<DashboardPage> = TestBed.createComponent(DashboardPage);
    second.detectChanges();
    expect((second.nativeElement as HTMLElement).textContent).toContain('Could not read card bills. Try again.');
  });
  it('expands a USD card and formats its purchases as USD, not ARS', () => {
    cardDueByMonth.and.returnValue(of([
      { bucket: 'Accrued', card: 'Amex', cycleYear: null, cycleMonth: null, amountMinorUnits: money(5000), currencyCode: 'USD', cardId: 'c5' }
    ]));
    cardPurchases.and.returnValue(of([
      { planId: 'p9', description: 'Headphones', totalMinorUnits: money(12000), installmentCount: 3, outstandingCount: 2, purchaseDate: '2026-06-01' }
    ]));
    setup();
    fixture.detectChanges();
    view.toggleCardPurchases('c5');
    fixture.detectChanges();
    const details: HTMLElement = pageEl().querySelector('#card-purchases-c5') as HTMLElement;
    expect(details.textContent).toContain(formatMoney(money(12000), 'USD'));
    expect(details.textContent).not.toContain(formatArs(money(12000)));
  });

  describe('month-driven view', () => {
    const pastMonth = '2020-03';
    const futureMonth = '2099-12';

    it('puts the month picker in the quick-actions nav, not in the flow section', () => {
      setup();
      fixture.detectChanges();
      const input: HTMLInputElement | null = pageEl().querySelector('nav[aria-label="Quick actions"] input[type="month"]');
      expect(input).toBeTruthy();
      expect(input?.value).toBe(monthKey(0));
      expect(pageEl().querySelector('section[aria-labelledby="flow-tile-label"] input[type="month"]')).toBeNull();
    });
    it('titles the page "This month" for the current month', () => {
      setup();
      fixture.detectChanges();
      expect(h1()).toBe('This month');
    });
    it('titles the page "<Month> <Year>" for another month', () => {
      setup();
      fixture.detectChanges();
      setMonth(pastMonth);
      expect(h1()).toBe('March 2020');
      setMonth(futureMonth);
      expect(h1()).toBe('December 2099');
      setMonth(monthKey(0));
      expect(h1()).toBe('This month');
    });
    it('ignores an empty month selection', () => {
      setup();
      fixture.detectChanges();
      view.onMonthChange('');
      expect(view.selectedMonth()).toBe(monthKey(0));
      expect(monthlyExpenses).toHaveBeenCalledTimes(1);
      expect(monthlyIncomes).toHaveBeenCalledTimes(1);
      expect(dueThisMonth).toHaveBeenCalledTimes(1);
      expect(listByMonth).toHaveBeenCalledTimes(1);
    });
    it('passes the picked month to every month-scoped feed and leaves card bills alone', () => {
      setup();
      fixture.detectChanges();
      setMonth(pastMonth);
      expect(monthlyExpenses.calls.mostRecent().args).toEqual([pastMonth]);
      expect(monthlyIncomes.calls.mostRecent().args).toEqual([pastMonth]);
      expect(dueThisMonth.calls.mostRecent().args).toEqual([pastMonth, localToday()]);
      expect(listByMonth.calls.mostRecent().args).toEqual([pastMonth]);
      expect(cardDueByMonth).toHaveBeenCalledTimes(1);
    });

    describe('card bills filtered client-side', () => {
      const rows: CardDueRow[] = [
        { bucket: 'Accrued', card: 'Visa', ...{ cycleYear: null, cycleMonth: null }, amountMinorUnits: money(100), currencyCode: 'ARS', cardId: 'c1' },
        { bucket: 'Future', card: 'Visa', ...cyc(0), amountMinorUnits: money(200), currencyCode: 'ARS', cardId: 'c1' },
        { bucket: 'Future', card: 'Visa', ...cyc(1), amountMinorUnits: money(400), currencyCode: 'ARS', cardId: 'c1' },
        { bucket: 'Future', card: 'Amex', ...cyc(1), amountMinorUnits: money(800), currencyCode: 'ARS', cardId: 'c2' }
      ];
      const totals = (): Record<string, number> =>
        Object.fromEntries(view.cycleByCard().map((card) => [card.card, card.total]));

      beforeEach(() => cardDueByMonth.and.returnValue(of(rows)));

      it('for the current month keeps its cycle rows plus the null-cycle Accrued rows', () => {
        setup();
        fixture.detectChanges();
        expect(totals()).toEqual({ Visa: 300 });
      });
      it('for another month keeps only rows of that cycle and drops null-cycle Accrued rows', () => {
        setup();
        fixture.detectChanges();
        setMonth(monthKey(1));
        expect(totals()).toEqual({ Visa: 400, Amex: 800 });
        expect(cardDueByMonth).toHaveBeenCalledTimes(1);
      });
      it('shows the empty copy for a month with no cycle rows', () => {
        setup();
        fixture.detectChanges();
        setMonth(pastMonth);
        expect(view.cycleByCard()).toEqual([]);
        expect(pageEl().textContent).toContain('Nothing due on your cards.');
      });
    });

    describe('stale responses', () => {
      it('drops a late success for a month that is no longer selected', () => {
        const expenses = byMonthSubjects(monthlyExpenses);
        const incomes = byMonthSubjects(monthlyIncomes);
        const due = byMonthSubjects(dueThisMonth as jasmine.Spy<(month: string) => Observable<DueThisMonthRow[]>>);
        const subs = byMonthSubjects(listByMonth);
        setup();
        fixture.detectChanges();
        const current: string = monthKey(0);
        setMonth(pastMonth);
        expenses.get(current)?.next(monthlyRows);
        incomes.get(current)?.next(incomeRows);
        due.get(current)?.next(dueRows);
        subs.get(current)?.next(monthSubs);
        expect(view.monthlyStatus()).toBe('loading');
        expect(view.incomeStatus()).toBe('loading');
        expect(view.dueStatus()).toBe('loading');
        expect(view.subscriptionsStatus()).toBe('loading');
        expect(view.expensesByCategory()).toEqual([]);
        expect(view.monthSubscriptions()).toEqual([]);
        expenses.get(pastMonth)?.next([]);
        due.get(pastMonth)?.next([]);
        subs.get(pastMonth)?.next([]);
        expect(view.monthlyStatus()).toBe('ready');
        expect(view.dueStatus()).toBe('ready');
        expect(view.subscriptionsStatus()).toBe('ready');
      });
      it('drops a late error for a month that is no longer selected', () => {
        const appError: AppError = { code: 'Http.ServerError', title: 'Server error', detail: 'boom', status: 500, metadata: {} };
        const expenses = byMonthSubjects(monthlyExpenses);
        const incomes = byMonthSubjects(monthlyIncomes);
        const due = byMonthSubjects(dueThisMonth as jasmine.Spy<(month: string) => Observable<DueThisMonthRow[]>>);
        const subs = byMonthSubjects(listByMonth);
        setup();
        fixture.detectChanges();
        const current: string = monthKey(0);
        setMonth(pastMonth);
        expenses.get(current)?.error(appError);
        incomes.get(current)?.error(appError);
        due.get(current)?.error(appError);
        subs.get(current)?.error(appError);
        expect(view.monthlyStatus()).toBe('loading');
        expect(view.incomeStatus()).toBe('loading');
        expect(view.dueStatus()).toBe('loading');
        expect(view.subscriptionsStatus()).toBe('loading');
      });
      it('still applies an error for the month that is selected', () => {
        const appError: AppError = { code: 'Http.ServerError', title: 'Server error', detail: 'boom', status: 500, metadata: {} };
        const expenses = byMonthSubjects(monthlyExpenses);
        const incomes = byMonthSubjects(monthlyIncomes);
        const due = byMonthSubjects(dueThisMonth as jasmine.Spy<(month: string) => Observable<DueThisMonthRow[]>>);
        const subs = byMonthSubjects(listByMonth);
        setup();
        fixture.detectChanges();
        setMonth(pastMonth);
        expenses.get(pastMonth)?.error(appError);
        incomes.get(pastMonth)?.error(appError);
        due.get(pastMonth)?.error(appError);
        subs.get(pastMonth)?.error(appError);
        expect(view.monthlyStatus()).toBe('error');
        expect(view.incomeStatus()).toBe('error');
        expect(view.dueStatus()).toBe('error');
        expect(view.subscriptionsStatus()).toBe('error');
      });
    });

    describe('Due card copy', () => {
      it('asks the total-owed question for the current month', () => {
        setup();
        fixture.detectChanges();
        expect(dueHeading()).toBe('How much do I owe in total for this month?');
      });
      it('asks the installments-only question for another month', () => {
        setup();
        fixture.detectChanges();
        setMonth(futureMonth);
        expect(dueHeading()).toBe('How much would I owe that month, based only on installments?');
        setMonth(pastMonth);
        expect(dueHeading()).toBe('How much would I owe that month, based only on installments?');
      });
      it('uses the short empty copy for another month and the "this month" copy for the current one', () => {
        setup();
        fixture.detectChanges();
        expect(dueCard().textContent).toContain('Nothing to pay this month.');
        setMonth(futureMonth);
        expect(dueCard().textContent).toContain('Nothing to pay.');
        expect(dueCard().textContent).not.toContain('Nothing to pay this month.');
      });
    });

    describe('subscriptions card', () => {
      const sub = (id: string, name: string, status: MonthSubscription['status'], dueDate: string): MonthSubscription => ({
        ...monthSubs[0], subscriptionId: id, name, status, dueDate
      });

      it('shows each row due date', () => {
        listByMonth.and.returnValue(of([sub('a', 'A', 'upcoming', '2026-10-17')]));
        setup();
        fixture.detectChanges();
        expect(subsCard().textContent).toContain('2026-10-17');
      });
      it('lists overdue rows first and keeps the original order otherwise', () => {
        listByMonth.and.returnValue(of([
          sub('a', 'A', 'upcoming', '2026-09-20'),
          sub('b', 'B', 'overdue', '2026-09-01'),
          sub('c', 'C', 'paid', '2026-09-05'),
          sub('d', 'D', 'overdue', '2026-09-02')
        ]));
        setup();
        fixture.detectChanges();
        expect(subNames()).toEqual(['B', 'D', 'A', 'C']);
      });
      it('notes future payments only for a future month', () => {
        setup();
        fixture.detectChanges();
        const note = 'Future payments — not charged yet';
        expect(subsCard().textContent).not.toContain(note);
        setMonth(futureMonth);
        expect(subsCard().textContent).toContain(note);
        setMonth(pastMonth);
        expect(subsCard().textContent).not.toContain(note);
        setMonth(monthKey(0));
        expect(subsCard().textContent).not.toContain(note);
      });
    });
  });
});
