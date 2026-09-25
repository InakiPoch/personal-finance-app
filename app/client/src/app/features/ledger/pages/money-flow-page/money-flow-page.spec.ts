import { provideZonelessChangeDetection } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Observable, of, throwError } from 'rxjs';
import { AppError } from '../../../../core/types/app-error';
import { Money } from '../../../../core/types/money';
import { ReportsService } from '../../../reports/reports-service';
import { MoneyFlowRow } from '../../types/money-flow-row';
import { MoneyFlowPage } from './money-flow-page';

type MoneyFlowView = {
  selectedMonth: () => string;
  loadStatus: () => 'loading' | 'ready' | 'error';
  footerTotals: () => Array<{ currencyCode: string; income: number; outcome: number }>;
  onMonthChange: (month: string) => void;
};

function currentMonthKey(): string {
  const now: Date = new Date();
  return `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}`;
}

describe('MoneyFlowPage', () => {
  let fixture: ComponentFixture<MoneyFlowPage>;
  let view: MoneyFlowView;
  let moneyFlow: jasmine.Spy<(month: string) => Observable<MoneyFlowRow[]>>;

  const money = (value: number): Money => value as Money;

  const rows: MoneyFlowRow[] = [
    {
      transactionId: 'tx-1',
      date: '2026-09-24',
      description: 'Salary September',
      accountName: 'Galicia',
      kind: 'Income',
      amountMinorUnits: money(85000000),
      currencyCode: 'ARS'
    },
    {
      transactionId: 'tx-2',
      date: '2026-09-22',
      description: 'Groceries at Coto',
      accountName: 'Galicia',
      kind: 'Outcome',
      amountMinorUnits: money(4530000),
      currencyCode: 'ARS'
    }
  ];

  function setup(): void {
    fixture = TestBed.createComponent(MoneyFlowPage);
    view = fixture.componentInstance as unknown as MoneyFlowView;
  }

  beforeEach(() => {
    moneyFlow = jasmine.createSpy('moneyFlow').and.returnValue(of(rows));
    TestBed.configureTestingModule({
      imports: [MoneyFlowPage],
      providers: [
        provideZonelessChangeDetection(),
        provideRouter([]),
        { provide: ReportsService, useValue: { moneyFlow } }
      ]
    });
  });

  it('defaults the month picker to the current month and fetches it', () => {
    setup();
    fixture.detectChanges();
    expect(view.selectedMonth()).toBe(currentMonthKey());
    expect(moneyFlow).toHaveBeenCalledOnceWith(currentMonthKey());
  });
  it('refetches when the month changes', () => {
    setup();
    fixture.detectChanges();
    view.onMonthChange('2026-01');
    expect(view.selectedMonth()).toBe('2026-01');
    expect(moneyFlow).toHaveBeenCalledTimes(2);
    expect(moneyFlow.calls.mostRecent().args).toEqual(['2026-01']);
  });
  it('shows the empty copy when no rows are returned', () => {
    moneyFlow.and.returnValue(of([]));
    setup();
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('No money moved this month.');
  });
  it('shows an error state when the feed fails', () => {
    const appError: AppError = { code: 'Http.ServerError', title: 'Server error', detail: 'boom', status: 500, metadata: {} };
    moneyFlow.and.returnValue(throwError(() => appError));
    setup();
    fixture.detectChanges();
    expect(view.loadStatus()).toBe('error');
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Could not load the money flow');
  });
  it('computes footer totals per currency, never summed across currencies', () => {
    moneyFlow.and.returnValue(of([
      ...rows,
      {
        transactionId: 'tx-3',
        date: '2026-09-20',
        description: 'Freelance gig',
        accountName: 'Galicia',
        kind: 'Income',
        amountMinorUnits: money(5000),
        currencyCode: 'USD'
      }
    ]));
    setup();
    fixture.detectChanges();
    const totals = view.footerTotals();
    expect(totals.length).toBe(2);
    const ars = totals.find((total) => total.currencyCode === 'ARS');
    const usd = totals.find((total) => total.currencyCode === 'USD');
    expect(ars?.income).toBe(85000000);
    expect(ars?.outcome).toBe(4530000);
    expect(usd?.income).toBe(5000);
    expect(usd?.outcome).toBe(0);
  });
});
