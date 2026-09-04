import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideZonelessChangeDetection } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { baseUrlInterceptor } from '../../core/http/base-url.interceptor';
import { problemDetailsInterceptor } from '../../core/http/problem-details.interceptor';
import { AppError } from '../../core/types/app-error';
import { Money } from '../../core/types/money';
import { environment } from '../../environments/environment';
import { CardFutureSchedule } from './types/card-future-schedule';
import { CreatePaymentPlan } from './types/create-payment-plan';
import { CreatePaymentPlanResult } from './types/create-payment-plan-result';
import { MonthlyStatement } from './types/monthly-statement';
import { MonthlyStatementSummary } from './types/monthly-statement-summary';
import { PayStatement } from './types/pay-statement';
import { PayStatementResult } from './types/pay-statement-result';
import { FinancingService } from './financing-service';

describe('FinancingService', () => {
  let service: FinancingService;
  let httpMock: HttpTestingController;

  const base: string = environment.apiUrl;
  const money = (value: number): Money => value as Money;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideZonelessChangeDetection(),
        provideHttpClient(withInterceptors([baseUrlInterceptor, problemDetailsInterceptor])),
        provideHttpClientTesting(),
      ]
    });
    service = TestBed.inject(FinancingService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('POSTs a payment plan without split and returns the plan id', () => {
    const body: CreatePaymentPlan = {
      amountMinorUnits: money(1200000),
      cardId: 'card-1',
      installmentCount: 3,
      purchaseDate: '2026-09-01',
      description: 'New laptop'
    };
    let result: string | undefined;
    service.createPaymentPlan(body).subscribe((r: CreatePaymentPlanResult) => (result = r.paymentPlanId));
    const req = httpMock.expectOne(`${base}/financing/payment-plans`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(body);
    expect('split' in (req.request.body as object)).toBe(false);
    req.flush({ paymentPlanId: 'plan-1' });
    expect(result).toBe('plan-1');
  });
  it('POSTs a payment plan carrying the split array when provided', () => {
    const body: CreatePaymentPlan = {
      amountMinorUnits: money(900000),
      cardId: 'card-1',
      installmentCount: 1,
      purchaseDate: '2026-09-01',
      description: 'Concert tickets',
      split: [
        { partyId: 'p1', weight: 1 },
        { partyId: 'p2', weight: 2 }
      ]
    };
    service.createPaymentPlan(body).subscribe();
    const req = httpMock.expectOne(`${base}/financing/payment-plans`);
    expect(req.request.body).toEqual(body);
    req.flush({ paymentPlanId: 'plan-2' });
  });
  it('GETs a statement and returns the object with installments intact', () => {
    const statement: MonthlyStatement = {
      statementId: 'st-1',
      cardId: 'card-1',
      cardName: 'Visa',
      cycleYear: 2026,
      cycleMonth: 9,
      amountDueMinorUnits: money(400000),
      isPaid: false,
      paidOnUtc: null,
      installments: [{
        planId: 'plan-1',
        installmentId: 'inst-1',
        sequence: 1,
        installmentCount: 3,
        purchaseDate: '2026-09-01',
        cycleYear: 2026,
        cycleMonth: 9,
        amountMinorUnits: money(400000),
        isReversed: false,
        reversalTransactionId: 'tx-acc-1'
      }]
    };
    let result: MonthlyStatement | undefined;
    service.getStatement('st-1').subscribe((r: MonthlyStatement) => (result = r));
    const req = httpMock.expectOne(`${base}/financing/statements/st-1`);
    expect(req.request.method).toBe('GET');
    req.flush(statement);
    expect(result).toEqual(statement);
  });
  it('POSTs a statement payment and returns the statement id', () => {
    const body: PayStatement = { bankAccountId: 'bank-1', paidOnUtc: '2026-09-20T12:00:00Z' };
    let result: string | undefined;
    service.payStatement('st-1', body).subscribe((r: PayStatementResult) => (result = r.statementId));
    const req = httpMock.expectOne(`${base}/financing/statements/st-1/pay`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(body);
    req.flush({ statementId: 'st-1' });
    expect(result).toBe('st-1');
  });
  it('maps a 409 AlreadyPaid on pay to an AppError keyed off code', () => {
    let error: AppError | undefined;
    service.payStatement('st-1', { bankAccountId: 'bank-1', paidOnUtc: '2026-09-20T12:00:00Z' })
      .subscribe({ next: () => {}, error: (e: AppError) => (error = e) });
    httpMock.expectOne(`${base}/financing/statements/st-1/pay`).flush(
      { title: 'Conflict', status: 409, detail: 'already paid', code: 'Financing.AlreadyPaid' },
      { status: 409, statusText: 'Conflict' }
    );
    expect(error).toEqual({
      code: 'Financing.AlreadyPaid',
      title: 'Conflict',
      detail: 'already paid',
      status: 409,
      metadata: {}
    });
  });
  it('GETs a card future schedule and returns the object with rows intact', () => {
    const schedule: CardFutureSchedule = {
      cardId: 'card-1',
      rows: [{
        planId: 'plan-1',
        installmentId: 'inst-2',
        sequence: 2,
        cycleYear: 2026,
        cycleMonth: 10,
        amountMinorUnits: money(400000)
      }]
    };
    let result: CardFutureSchedule | undefined;
    service.getFutureSchedule('card-1').subscribe((r: CardFutureSchedule) => (result = r));
    const req = httpMock.expectOne(`${base}/financing/cards/card-1/future-schedule`);
    expect(req.request.method).toBe('GET');
    req.flush(schedule);
    expect(result).toEqual(schedule);
  });
  it('GETs a card statement list and unwraps the { rows } envelope', () => {
    const rows: MonthlyStatementSummary[] = [{
      statementId: 'st-1',
      cardId: 'card-1',
      cardName: 'Visa',
      cycleYear: 2026,
      cycleMonth: 9,
      amountDueMinorUnits: money(400000),
      isPaid: false,
      paidOnUtc: null
    }];
    let result: MonthlyStatementSummary[] | undefined;
    service.listStatements('card-1').subscribe((r: MonthlyStatementSummary[]) => (result = r));
    const req = httpMock.expectOne(`${base}/financing/cards/card-1/statements`);
    expect(req.request.method).toBe('GET');
    req.flush({ cardId: 'card-1', rows });
    expect(result).toEqual(rows);
  });
  it('maps a 404 on getStatement to an AppError keyed off code', () => {
    let error: AppError | undefined;
    service.getStatement('missing').subscribe({ next: () => {}, error: (e: AppError) => (error = e) });
    httpMock.expectOne(`${base}/financing/statements/missing`).flush(
      { title: 'Not found', status: 404, detail: 'no such statement', code: 'Financing.StatementNotFound' },
      { status: 404, statusText: 'Not Found' }
    );
    expect(error?.code).toBe('Financing.StatementNotFound');
    expect(error?.status).toBe(404);
  });
});
