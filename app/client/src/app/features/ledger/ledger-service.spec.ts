import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideZonelessChangeDetection } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { baseUrlInterceptor } from '../../core/http/base-url.interceptor';
import { problemDetailsInterceptor } from '../../core/http/problem-details.interceptor';
import { AppError } from '../../core/types/app-error';
import { Money } from '../../core/types/money';
import { environment } from '../../environments/environment';
import { AccountBalance } from './types/account-balance';
import { PostTransaction } from './types/post-transaction';
import { PostTransactionResult } from './types/post-transaction-result';
import { ReverseTransactionResult } from './types/reverse-transaction-result';
import { LedgerService } from './ledger-service';

describe('LedgerService', () => {
  let service: LedgerService;
  let httpMock: HttpTestingController;

  const base: string = environment.apiUrl;
  const money = (value: number): Money => value as Money;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideZonelessChangeDetection(),
        provideHttpClient(withInterceptors([baseUrlInterceptor, problemDetailsInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    service = TestBed.inject(LedgerService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('POSTs a low-level transaction and returns the transaction id', () => {
    const body: PostTransaction = {
      lines: [
        { accountId: 'acc-1', direction: 'Debit', amountMinorUnits: money(50000) },
        { accountId: 'acc-2', direction: 'Credit', amountMinorUnits: money(50000) },
      ],
      postedOnUtc: '2026-09-15T10:30:00Z',
      description: 'manual'
    };
    let result: string | undefined;
    service.postTransaction(body).subscribe((r: PostTransactionResult) => (result = r.transactionId));
    const req = httpMock.expectOne(`${base}/ledger/transactions`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(body);
    req.flush({ transactionId: 'tx-1' });
    expect(result).toBe('tx-1');
  });

  it('POSTs a reversal for a transaction id and returns the append-only result', () => {
    let result: ReverseTransactionResult | undefined;
    service.reverse('tx-1').subscribe((r: ReverseTransactionResult) => (result = r));
    const req = httpMock.expectOne(`${base}/ledger/transactions/tx-1/reversal`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({});
    req.flush({
      reversalTransactionId: 'tx-2',
      originalTransactionId: 'tx-1',
      compensatingEntryPosted: true,
    });
    expect(result).toEqual({
      reversalTransactionId: 'tx-2',
      originalTransactionId: 'tx-1',
      compensatingEntryPosted: true
    });
  });
  it('GETs an account balance and returns the object as-is', () => {
    const balance: AccountBalance = {
      accountId: 'acc-1',
      balanceMinorUnits: money(125000),
      currencyCode: 'ARS',
      formatted: '$ 1.250,00'
    };
    let result: AccountBalance | undefined;
    service.getAccountBalance('acc-1').subscribe((r: AccountBalance) => (result = r));
    const req = httpMock.expectOne(`${base}/ledger/accounts/acc-1/balance`);
    expect(req.request.method).toBe('GET');
    req.flush(balance);
    expect(result).toEqual(balance);
  });
  it('maps a 409 CannotReverseAReversal to an AppError keyed off code', () => {
    let error: AppError | undefined;
    service.reverse('tx-2').subscribe({ next: () => {}, error: (e: AppError) => (error = e) });
    httpMock.expectOne(`${base}/ledger/transactions/tx-2/reversal`).flush(
      { title: 'Conflict', status: 409, detail: 'Ledger.CannotReverseAReversal', code: 'Ledger.CannotReverseAReversal' },
      { status: 409, statusText: 'Conflict' }
    );
    expect(error?.code).toBe('Ledger.CannotReverseAReversal');
    expect(error?.status).toBe(409);
  });
});
