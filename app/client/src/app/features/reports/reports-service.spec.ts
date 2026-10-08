import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideZonelessChangeDetection } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { baseUrlInterceptor } from '../../core/http/base-url.interceptor';
import { problemDetailsInterceptor } from '../../core/http/problem-details.interceptor';
import { AppError } from '../../core/types/app-error';
import { Money } from '../../core/types/money';
import { environment } from '../../environments/environment';
import { MoneyFlowRow } from '../ledger/types/money-flow-row';
import { CardDueRow } from './types/card-due-row';
import { MonthlyExpenseRow } from './types/monthly-expense-row';
import { MonthlyIncomeRow } from './types/monthly-income-row';
import { OwedToYouRow } from './types/owed-to-you-row';
import { PartyDebtRow } from './types/party-debt-row';
import { PartyTimelineRow } from './types/party-timeline-row';
import { TransactionFeedRow } from './types/transaction-feed-row';
import { ReportsService } from './reports-service';

describe('ReportsService', () => {
  let service: ReportsService;
  let httpMock: HttpTestingController;

  const monthlyUrl: string = `${environment.apiUrl}/reports/monthly-expenses`;
  const monthlyIncomesUrl: string = `${environment.apiUrl}/reports/monthly-incomes`;
  const cardDueUrl: string = `${environment.apiUrl}/reports/card-due-by-month`;
  const moneyFlowUrl: string = `${environment.apiUrl}/reports/money-flow`;
  const debtSummaryUrl: string = `${environment.apiUrl}/reports/parties/debt-summary`;
  const money = (value: number): Money => value as Money;

  const monthlyRows: MonthlyExpenseRow[] = [
    { month: '2026-09', category: 'Groceries', amountMinorUnits: money(150000), currencyCode: 'ARS' }
  ];
  const cardDueRows: CardDueRow[] = [{
    bucket: 'Accrued',
    card: 'Visa',
    cycleYear: 2026,
    cycleMonth: 9,
    amountMinorUnits: money(500000),
    currencyCode: 'ARS',
    cardId: 'c1'
  }];

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideZonelessChangeDetection(),
        provideHttpClient(withInterceptors([baseUrlInterceptor, problemDetailsInterceptor])),
        provideHttpClientTesting()
      ]
    });
    service = TestBed.inject(ReportsService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('GETs monthly expenses without a month param and unwraps { rows }', () => {
    let result: MonthlyExpenseRow[] | undefined;
    service.monthlyExpenses().subscribe((rows: MonthlyExpenseRow[]) => (result = rows));
    const req = httpMock.expectOne(monthlyUrl);
    expect(req.request.method).toBe('GET');
    expect(req.request.params.has('month')).toBe(false);
    req.flush({ rows: monthlyRows });
    expect(result).toEqual(monthlyRows);
  });
  it('passes the month as a query param when provided', () => {
    service.monthlyExpenses('2026-09').subscribe();
    const req = httpMock.expectOne((r) => r.url === monthlyUrl);
    expect(req.request.params.get('month')).toBe('2026-09');
    req.flush({ rows: [] });
  });
  it('GETs monthly incomes without a month param and unwraps { rows }', () => {
    const incomeRows: MonthlyIncomeRow[] = [
      { month: '2026-09', amountMinorUnits: money(50000), currencyCode: 'ARS' }
    ];
    let result: MonthlyIncomeRow[] | undefined;
    service.monthlyIncomes().subscribe((rows: MonthlyIncomeRow[]) => (result = rows));
    const req = httpMock.expectOne(monthlyIncomesUrl);
    expect(req.request.method).toBe('GET');
    expect(req.request.params.has('month')).toBe(false);
    req.flush({ rows: incomeRows });
    expect(result).toEqual(incomeRows);
  });
  it('passes the month as a query param to monthly incomes when provided', () => {
    service.monthlyIncomes('2026-09').subscribe();
    const req = httpMock.expectOne((r) => r.url === monthlyIncomesUrl);
    expect(req.request.params.get('month')).toBe('2026-09');
    req.flush({ rows: [] });
  });
  it('GETs money-flow with the month param and unwraps { rows }', () => {
    const flowRows: MoneyFlowRow[] = [{
      transactionId: 'tx-1',
      date: '2026-09-24',
      description: 'Salary September',
      accountName: 'Galicia',
      kind: 'Income',
      amountMinorUnits: money(85000000),
      currencyCode: 'ARS'
    }];
    let result: MoneyFlowRow[] | undefined;
    service.moneyFlow('2026-09').subscribe((rows: MoneyFlowRow[]) => (result = rows));
    const req = httpMock.expectOne((r) => r.url === moneyFlowUrl);
    expect(req.request.method).toBe('GET');
    expect(req.request.params.get('month')).toBe('2026-09');
    req.flush({ rows: flowRows });
    expect(result).toEqual(flowRows);
  });
  it('GETs card-due-by-month and unwraps { rows }', () => {
    let result: CardDueRow[] | undefined;
    service.cardDueByMonth().subscribe((rows: CardDueRow[]) => (result = rows));
    const req = httpMock.expectOne(cardDueUrl);
    expect(req.request.method).toBe('GET');
    req.flush({ rows: cardDueRows });
    expect(result).toEqual(cardDueRows);
  });
  it('GETs parties/owed-to-you with month and today and unwraps { rows }', () => {
    const rows: OwedToYouRow[] = [
      { partyId: 'p1', partyName: 'Alice', currencyCode: 'ARS', amountMinorUnits: money(250000) },
    ];
    let result: OwedToYouRow[] | undefined;
    service.owedToYou('2026-10', '2026-10-08').subscribe((r: OwedToYouRow[]) => (result = r));
    const req = httpMock.expectOne((request) => request.url === `${environment.apiUrl}/reports/parties/owed-to-you`);
    expect(req.request.method).toBe('GET');
    expect(req.request.params.get('month')).toBe('2026-10');
    expect(req.request.params.get('today')).toBe('2026-10-08');
    req.flush({ rows });
    expect(result).toEqual(rows);
  });
  it('GETs parties/debt-summary and unwraps { rows }', () => {
    const rows: PartyDebtRow[] = [
      { partyId: 'p1', partyName: 'Alice', netBalanceMinorUnits: money(250000), currencyCode: 'ARS' },
    ];
    let result: PartyDebtRow[] | undefined;
    service.debtSummary().subscribe((r: PartyDebtRow[]) => (result = r));
    const req = httpMock.expectOne(debtSummaryUrl);
    expect(req.request.method).toBe('GET');
    req.flush({ rows });
    expect(result).toEqual(rows);
  });
  it('GETs parties/{id}/timeline and unwraps { rows }', () => {
    const rows: PartyTimelineRow[] = [
      {
        transactionId: 'tx-1',
        movementOnUtc: '2026-09-01T20:00:00.000Z',
        description: 'Dinner split',
        deltaMinorUnits: money(300000),
        runningBalanceMinorUnits: money(300000),
        currencyCode: 'ARS'
      },
    ];
    let result: PartyTimelineRow[] | undefined;
    service.partyTimeline('p1').subscribe((r: PartyTimelineRow[]) => (result = r));
    const req = httpMock.expectOne(`${environment.apiUrl}/reports/parties/p1/timeline`);
    expect(req.request.method).toBe('GET');
    req.flush({ rows });
    expect(result).toEqual(rows);
  });
  const feedRow: TransactionFeedRow = {
    id: 'tx-1',
    postedOnUtc: '2026-09-15T10:30:00Z',
    kind: 'Income',
    description: 'Salary',
    fromAccounts: ['Salary'],
    toAccounts: ['Checking'],
    amountMinorUnits: money(90000000),
    currencyCode: 'ARS',
    isUndoEntry: false,
    isUndone: false,
    impactLines: ['ARS 900.000 is removed from Checking.']
  };
  it('GETs the transaction feed without params and unwraps { rows }', () => {
    let result: TransactionFeedRow[] | undefined;
    service.transactions().subscribe((r: TransactionFeedRow[]) => (result = r));
    const req = httpMock.expectOne((r) => r.url === `${environment.apiUrl}/reports/transactions`);
    expect(req.request.method).toBe('GET');
    expect(req.request.params.keys()).toEqual([]);
    req.flush({ rows: [feedRow] });
    expect(result).toEqual([feedRow]);
  });
  it('passes the transaction feed account and date filter as query params', () => {
    service.transactions({ accountId: 'acc-1', from: '2026-09-01', to: '2026-09-30' }).subscribe();
    const req = httpMock.expectOne((r) => r.url === `${environment.apiUrl}/reports/transactions`);
    expect(req.request.params.get('accountId')).toBe('acc-1');
    expect(req.request.params.get('from')).toBe('2026-09-01');
    expect(req.request.params.get('to')).toBe('2026-09-30');
    req.flush({ rows: [] });
  });
  it('GETs a single transaction by id and returns the row as-is', () => {
    let result: TransactionFeedRow | undefined;
    service.transaction('tx-1').subscribe((r: TransactionFeedRow) => (result = r));
    const req = httpMock.expectOne(`${environment.apiUrl}/reports/transactions/tx-1`);
    expect(req.request.method).toBe('GET');
    req.flush(feedRow);
    expect(result).toEqual(feedRow);
  });
  it('maps a failed response to an AppError keyed off code', () => {
    let error: AppError | undefined;
    service.monthlyExpenses().subscribe({ next: () => {}, error: (e: AppError) => (error = e) });
    httpMock.expectOne(monthlyUrl).flush(
      { type: 'about:blank', title: 'Unprocessable', status: 422, detail: 'bad month', code: 'Reports.InvalidMonth' },
      { status: 422, statusText: 'Unprocessable Content' }
    );
    expect(error).toEqual({
      code: 'Reports.InvalidMonth',
      title: 'Unprocessable',
      detail: 'bad month',
      status: 422,
      metadata: {}
    });
  });
});
