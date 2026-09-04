import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideZonelessChangeDetection } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { baseUrlInterceptor } from '../../core/http/base-url.interceptor';
import { problemDetailsInterceptor } from '../../core/http/problem-details.interceptor';
import { AppError } from '../../core/types/app-error';
import { Money } from '../../core/types/money';
import { environment } from '../../environments/environment';
import { CardDueRow } from './types/card-due-row';
import { MonthlyExpenseRow } from './types/monthly-expense-row';
import { PartyDebtRow } from './types/party-debt-row';
import { PartyTimelineRow } from './types/party-timeline-row';
import { ReportsService } from './reports-service';

describe('ReportsService', () => {
  let service: ReportsService;
  let httpMock: HttpTestingController;

  const monthlyUrl: string = `${environment.apiUrl}/reports/monthly-expenses`;
  const cardDueUrl: string = `${environment.apiUrl}/reports/card-due-by-month`;
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
  it('GETs card-due-by-month and unwraps { rows }', () => {
    let result: CardDueRow[] | undefined;
    service.cardDueByMonth().subscribe((rows: CardDueRow[]) => (result = rows));
    const req = httpMock.expectOne(cardDueUrl);
    expect(req.request.method).toBe('GET');
    req.flush({ rows: cardDueRows });
    expect(result).toEqual(cardDueRows);
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
