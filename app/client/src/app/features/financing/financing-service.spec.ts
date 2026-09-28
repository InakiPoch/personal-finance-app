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
import { CreditorDetail } from './types/creditor-detail';
import { CreditorPayableRow } from './types/creditor-payable-row';
import { MonthlyStatement } from './types/monthly-statement';
import { MonthlyStatementSummary } from './types/monthly-statement-summary';
import { PayCreditorFullDebtResult } from './types/pay-creditor-full-debt-result';
import { PayCreditorInstallment } from './types/pay-creditor-installment';
import { PayCreditorInstallmentResult } from './types/pay-creditor-installment-result';
import { PayInstallment } from './types/pay-installment';
import { PayInstallmentResult } from './types/pay-installment-result';
import { PayStatement } from './types/pay-statement';
import { PayStatementResult } from './types/pay-statement-result';
import { RecentPurchaseRow } from './types/recent-purchase-row';
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
      currencyCode: 'ARS',
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
        reversalTransactionId: 'tx-acc-1',
        isPaid: false,
        paidOnUtc: null,
        currencyCode: 'ARS'
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
  it('POSTs an installment payment and returns the installment id', () => {
    const body: PayInstallment = { bankAccountId: 'bank-1', paidOnUtc: '2026-09-20T12:00:00Z' };
    let result: string | undefined;
    service.payInstallment('inst-1', body).subscribe((r: PayInstallmentResult) => (result = r.installmentId));
    const req = httpMock.expectOne(`${base}/financing/installments/inst-1/pay`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(body);
    req.flush({ installmentId: 'inst-1' });
    expect(result).toBe('inst-1');
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
      paidOnUtc: null,
      currencyCode: 'ARS'
    }];
    let result: MonthlyStatementSummary[] | undefined;
    service.listStatements('card-1').subscribe((r: MonthlyStatementSummary[]) => (result = r));
    const req = httpMock.expectOne(`${base}/financing/cards/card-1/statements`);
    expect(req.request.method).toBe('GET');
    req.flush({ cardId: 'card-1', rows });
    expect(result).toEqual(rows);
  });
  it('GETs the recent purchases list and unwraps the { rows } envelope', () => {
    const rows: RecentPurchaseRow[] = [{
      planId: 'plan-1',
      description: 'New laptop',
      cardName: 'Visa',
      purchaseDate: '2026-09-01',
      totalMinorUnits: money(1200000),
      installmentCount: 3,
      isCreditorPayment: false,
      paidInstallmentCount: 0,
      nextDueYear: null,
      nextDueMonth: null,
      pendingAmountMinorUnits: money(0),
      currencyCode: 'ARS'
    }];
    let result: RecentPurchaseRow[] | undefined;
    service.recentPurchases().subscribe((r: RecentPurchaseRow[]) => (result = r));
    const req = httpMock.expectOne(`${base}/financing/purchases/recent`);
    expect(req.request.method).toBe('GET');
    req.flush({ rows });
    expect(result).toEqual(rows);
  });
  it('GETs the creditor payables list and unwraps the { rows } envelope', () => {
    const rows: CreditorPayableRow[] = [{
      creditorId: 'cr-1',
      creditorName: 'Juan',
      dueNowMinorUnits: money(45000),
      totalOwedMinorUnits: money(45000),
      nextDueDate: '2026-03-10',
      accounts: [
        { accountId: 'acc-1', label: 'Galicia', outstandingMinorUnits: money(45000) }
      ]
    }];
    let result: CreditorPayableRow[] | undefined;
    service.creditorPayables().subscribe((r: CreditorPayableRow[]) => (result = r));
    const req = httpMock.expectOne(`${base}/financing/creditor-payables`);
    expect(req.request.method).toBe('GET');
    req.flush({ rows });
    expect(result).toEqual(rows);
  });
  it('GETs a creditor detail as a bare object with its purchase groups intact', () => {
    const detail: CreditorDetail = {
      creditorId: 'cr-1',
      creditorName: 'Juan',
      purchases: [{
        planId: 'pl-1',
        description: 'Sofa',
        purchaseDate: '2026-01-10',
        totalMinorUnits: money(300000),
        outstandingMinorUnits: money(200000),
        currencyCode: 'ARS',
        installments: [{
          installmentId: 'i-1',
          sequence: 1,
          installmentCount: 3,
          amountMinorUnits: money(100000),
          dueYear: 2026,
          dueMonth: 2,
          isPaid: false,
          isReversed: false,
          status: 'due',
          paidMinorUnits: money(0),
          remainingMinorUnits: money(100000),
          hasPayments: false
        }]
      }]
    };
    let result: CreditorDetail | undefined;
    service.creditorDetail('cr-1').subscribe((r: CreditorDetail) => (result = r));
    const req = httpMock.expectOne(`${base}/financing/creditor-payables/cr-1`);
    expect(req.request.method).toBe('GET');
    req.flush(detail);
    expect(result).toEqual(detail);
  });
  it('maps a 404 CreditorNotFound on creditorDetail to an AppError keyed off code', () => {
    let error: AppError | undefined;
    service.creditorDetail('missing').subscribe({ next: () => {}, error: (e: AppError) => (error = e) });
    httpMock.expectOne(`${base}/financing/creditor-payables/missing`).flush(
      { title: 'Not found', status: 404, detail: 'no such creditor', code: 'Financing.CreditorNotFound' },
      { status: 404, statusText: 'Not Found' }
    );
    expect(error?.code).toBe('Financing.CreditorNotFound');
    expect(error?.status).toBe(404);
  });
  it('POSTs the payment body to pay a creditor installment in full and returns its id', () => {
    let result: string | undefined;
    const body: PayCreditorInstallment = { amountMinorUnits: null };
    service.payCreditorInstallment('ci-1', body)
      .subscribe((r: PayCreditorInstallmentResult) => (result = r.installmentId));
    const req = httpMock.expectOne(`${base}/financing/creditor-installments/ci-1/pay`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(body);
    req.flush({ installmentId: 'ci-1' });
    expect(result).toBe('ci-1');
  });
  it('POSTs a custom minor-unit amount to pay a creditor installment', () => {
    const body: PayCreditorInstallment = { amountMinorUnits: money(15050) };
    service.payCreditorInstallment('ci-1', body).subscribe();
    const req = httpMock.expectOne(`${base}/financing/creditor-installments/ci-1/pay`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(body);
    req.flush({ installmentId: 'ci-1' });
  });
  it('POSTs an empty body to undo a creditor installment payment and returns its id', () => {
    let result: string | undefined;
    service.unpayCreditorInstallment('ci-1')
      .subscribe((r: PayCreditorInstallmentResult) => (result = r.installmentId));
    const req = httpMock.expectOne(`${base}/financing/creditor-installments/ci-1/unpay`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({});
    req.flush({ installmentId: 'ci-1' });
    expect(result).toBe('ci-1');
  });
  it('POSTs an empty body to pay a creditor full debt and returns the settled count', () => {
    let result: number | undefined;
    service.payCreditorFullDebt('cr-1')
      .subscribe((r: PayCreditorFullDebtResult) => (result = r.settledCount));
    const req = httpMock.expectOne(`${base}/financing/creditor-payables/cr-1/pay-full`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({});
    req.flush({ settledCount: 4 });
    expect(result).toBe(4);
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
