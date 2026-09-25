import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideZonelessChangeDetection } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { baseUrlInterceptor } from '../../core/http/base-url.interceptor';
import { problemDetailsInterceptor } from '../../core/http/problem-details.interceptor';
import { AppError } from '../../core/types/app-error';
import { Money } from '../../core/types/money';
import { environment } from '../../environments/environment';
import { CreateParty } from './types/create-party';
import { CurrentAccountBalance } from './types/current-account-balance';
import { CurrentAccountTimelineRow } from './types/current-account-timeline-row';
import { PartyResult } from './types/party-result';
import { PendingSharesByPartyRow } from './types/pending-shares-by-party-row';
import { RegisterSharedExpense } from './types/register-shared-expense';
import { SettleCurrentAccount } from './types/settle-current-account';
import { SettlementResult } from './types/settlement-result';
import { SharedExpenseResult } from './types/shared-expense-result';
import { PartiesService } from './parties-service';

describe('PartiesService', () => {
  let service: PartiesService;
  let httpMock: HttpTestingController;

  const money = (value: number): Money => value as Money;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideZonelessChangeDetection(),
        provideHttpClient(withInterceptors([baseUrlInterceptor, problemDetailsInterceptor])),
        provideHttpClientTesting()
      ],
    });
    service = TestBed.inject(PartiesService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('GETs the current-account balance for a party', () => {
    const balance: CurrentAccountBalance = {
      partyId: 'p1',
      name: 'Alice',
      balances: [{ currencyCode: 'ARS', balanceMinorUnits: money(250000) }]
    };
    let result: CurrentAccountBalance | undefined;
    service.getBalance('p1').subscribe((r: CurrentAccountBalance) => (result = r));
    const req = httpMock.expectOne(`${environment.apiUrl}/parties/p1/balance`);
    expect(req.request.method).toBe('GET');
    req.flush(balance);
    expect(result).toEqual(balance);
  });
  it('maps a 404 to an AppError keyed off code', () => {
    let error: AppError | undefined;
    service.getBalance('missing').subscribe({ next: () => {}, error: (e: AppError) => (error = e) });
    httpMock.expectOne(`${environment.apiUrl}/parties/missing/balance`).flush(
      { title: 'Not found', status: 404, detail: 'no such party', code: 'Parties.NotFound' },
      { status: 404, statusText: 'Not Found' }
    );
    expect(error?.code).toBe('Parties.NotFound');
    expect(error?.status).toBe(404);
  });
  it('POSTs the create-party body and returns the new id', () => {
    const body: CreateParty = { name: 'Bob' };
    let result: PartyResult | undefined;
    service.create(body).subscribe((created: PartyResult) => (result = created));
    const req = httpMock.expectOne(`${environment.apiUrl}/parties`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(body);
    req.flush({ id: 'p9' });
    expect(result).toEqual({ id: 'p9' });
  });
  it('maps a 422 on create to an AppError keyed off code', () => {
    let error: AppError | undefined;
    service.create({ name: '' }).subscribe({ next: () => {}, error: (e: AppError) => (error = e) });
    httpMock.expectOne(`${environment.apiUrl}/parties`).flush(
      { title: 'Unprocessable entity', status: 422, detail: 'name is required', code: 'Parties.InvalidName' },
      { status: 422, statusText: 'Unprocessable Entity' }
    );
    expect(error?.code).toBe('Parties.InvalidName');
    expect(error?.status).toBe(422);
  });
  it('POSTs a shared expense with the participants array intact', () => {
    const body: RegisterSharedExpense = {
      description: 'Dinner',
      totalMinorUnits: money(900000),
      expenseAccountId: 'exp-1',
      fundingAccountId: 'acc-1',
      incurredOnUtc: '2026-09-01T20:00:00.000Z',
      participants: [
        { partyId: 'p1', weight: 1 },
        { partyId: 'p2', weight: 2 }
      ],
      currencyCode: 'ARS'
    };
    let result: SharedExpenseResult | undefined;
    service.registerSharedExpense(body).subscribe((r: SharedExpenseResult) => (result = r));
    const req = httpMock.expectOne(`${environment.apiUrl}/parties/shared-expenses`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(body);
    req.flush({ splitReferenceId: 'split-1' });
    expect(result).toEqual({ splitReferenceId: 'split-1' });
  });
  it('maps a 422 on a shared expense to an AppError keyed off code', () => {
    let error: AppError | undefined;
    service
      .registerSharedExpense({
        description: 'Dinner',
        totalMinorUnits: money(900000),
        expenseAccountId: 'exp-1',
        fundingAccountId: 'acc-1',
        incurredOnUtc: '2026-09-01T20:00:00.000Z',
        participants: [],
        currencyCode: 'ARS'
      }).subscribe({ next: () => {}, error: (e: AppError) => (error = e) });
    httpMock.expectOne(`${environment.apiUrl}/parties/shared-expenses`).flush(
      {
        title: 'Unprocessable entity',
        status: 422,
        detail: 'participants required',
        code: 'Parties.InvalidParticipants'
      },
      { status: 422, statusText: 'Unprocessable Entity' }
    );
    expect(error?.code).toBe('Parties.InvalidParticipants');
    expect(error?.status).toBe(422);
  });
  it('POSTs a settlement for a party and returns the ledger transaction id', () => {
    const body: SettleCurrentAccount = {
      amountMinorUnits: money(250000),
      currencyCode: 'ARS',
      bankAccountId: 'acc-1',
      settledOnUtc: '2026-09-02T12:00:00.000Z'
    };
    let result: SettlementResult | undefined;
    service.settle('p1', body).subscribe((r: SettlementResult) => (result = r));
    const req = httpMock.expectOne(`${environment.apiUrl}/parties/p1/settlements`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(body);
    req.flush({ ledgerTransactionId: 'tx-1' });
    expect(result).toEqual({ ledgerTransactionId: 'tx-1' });
  });
  it('maps a 409 on a settlement to an AppError keyed off code', () => {
    let error: AppError | undefined;
    service
      .settle('p1', {
        amountMinorUnits: money(999999999),
        currencyCode: 'ARS',
        bankAccountId: 'acc-1',
        settledOnUtc: '2026-09-02T12:00:00.000Z'
      }).subscribe({ next: () => {}, error: (e: AppError) => (error = e) });
    httpMock.expectOne(`${environment.apiUrl}/parties/p1/settlements`).flush(
      {
        title: 'Conflict',
        status: 409,
        detail: 'settlement exceeds balance',
        code: 'Parties.SettlementExceedsBalance'
      },
      { status: 409, statusText: 'Conflict' }
    );
    expect(error?.code).toBe('Parties.SettlementExceedsBalance');
    expect(error?.status).toBe(409);
  });
  it('GETs the current-account timeline and unwraps { rows }', () => {
    const rows: CurrentAccountTimelineRow[] = [
      {
        transactionId: 'tx-1',
        movementOnUtc: '2026-09-01T20:00:00.000Z',
        description: 'Dinner split',
        deltaMinorUnits: money(300000),
        runningBalanceMinorUnits: money(300000),
        currencyCode: 'ARS'
      }
    ];
    let result: CurrentAccountTimelineRow[] | undefined;
    service.getTimeline('p1').subscribe((r: CurrentAccountTimelineRow[]) => (result = r));
    const req = httpMock.expectOne(`${environment.apiUrl}/parties/p1/timeline`);
    expect(req.request.method).toBe('GET');
    req.flush({ rows });
    expect(result).toEqual(rows);
  });
  it('maps a 404 on the timeline to an AppError keyed off code', () => {
    let error: AppError | undefined;
    service.getTimeline('missing').subscribe({ next: () => {}, error: (e: AppError) => (error = e) });
    httpMock.expectOne(`${environment.apiUrl}/parties/missing/timeline`).flush(
      { title: 'Not found', status: 404, detail: 'no such party', code: 'Parties.PartyNotFound' },
      { status: 404, statusText: 'Not Found' }
    );
    expect(error?.code).toBe('Parties.PartyNotFound');
    expect(error?.status).toBe(404);
  });
  it('GETs the pending scheduled shares per party and unwraps { rows }', () => {
    const rows: PendingSharesByPartyRow[] = [
      { partyId: 'p1', scheduledCount: 3, scheduledTotalMinorUnits: money(450000), currencyCode: 'ARS' }
    ];
    let result: PendingSharesByPartyRow[] | undefined;
    service.pendingShares().subscribe((r: PendingSharesByPartyRow[]) => (result = r));
    const req = httpMock.expectOne(`${environment.apiUrl}/parties/pending-shares`);
    expect(req.request.method).toBe('GET');
    req.flush({ rows });
    expect(result).toEqual(rows);
  });
});
