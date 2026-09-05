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
import { RecordDebitExpense } from './types/record-debit-expense';
import { RecordDebitExpenseResult } from './types/record-debit-expense-result';
import { ReverseTransactionResult } from './types/reverse-transaction-result';
import { TransactionRow } from './types/transaction-row';
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
        provideHttpClientTesting()
      ]
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

  it('GETs the transaction feed and unwraps the { rows } envelope', () => {
    const rows: TransactionRow[] = [{
      transactionId: 'tx-1',
      postedOnUtc: '2026-09-15T10:30:00Z',
      description: 'Manual entry',
      amountMinorUnits: money(50000),
      isReversal: false,
      isReversed: false,
      installmentReferenceId: null,
      splitReferenceId: null
    }];
    let result: TransactionRow[] | undefined;
    service.listTransactions().subscribe((r: TransactionRow[]) => (result = r));
    const req = httpMock.expectOne(
      (r) => r.method === 'GET' && r.url === `${base}/ledger/transactions`,
    );
    expect(req.request.params.keys()).toEqual([]);
    req.flush({ rows });
    expect(result).toEqual(rows);
  });

  it('passes the account and date-range filter through as query params', () => {
    let result: TransactionRow[] | undefined;
    service
      .listTransactions({ accountId: 'acc-1', from: '2026-09-01', to: '2026-09-30' })
      .subscribe((r: TransactionRow[]) => (result = r));
    const req = httpMock.expectOne(
      (r) => r.method === 'GET' && r.url === `${base}/ledger/transactions`,
    );
    expect(req.request.params.get('accountId')).toBe('acc-1');
    expect(req.request.params.get('from')).toBe('2026-09-01');
    expect(req.request.params.get('to')).toBe('2026-09-30');
    req.flush({ rows: [] });
    expect(result).toEqual([]);
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
      compensatingEntryPosted: true
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
  it('POSTs a debit expense and returns the recorded id', () => {
    const body: RecordDebitExpense = {
      amountMinorUnits: money(25000),
      sourceInstrumentId: 'acct-debit',
      categoryName: 'Groceries',
      purchaseDate: '2026-03-10',
      description: 'Weekly shop'
    };
    let result: string | undefined;
    service.recordDebitExpense(body).subscribe((r: RecordDebitExpenseResult) => (result = r.id));
    const req = httpMock.expectOne(`${base}/ledger/expenses`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(body);
    req.flush({ id: 'expense-1' });
    expect(result).toBe('expense-1');
  });

  it('POSTs a debit expense carrying a split payload', () => {
    const body: RecordDebitExpense = {
      amountMinorUnits: money(30000),
      sourceInstrumentId: 'acct-debit',
      categoryName: 'Dining',
      purchaseDate: '2026-03-10',
      description: 'Dinner',
      split: [{ partyId: 'p1', weight: 1 }]
    };
    service.recordDebitExpense(body).subscribe();
    const req = httpMock.expectOne(`${base}/ledger/expenses`);
    expect(req.request.body).toEqual(body);
    req.flush({ id: 'expense-2' });
  });

  it('GETs the expense categories and unwraps the { rows } envelope to names', () => {
    let result: string[] | undefined;
    service.listExpenseCategories().subscribe((r: string[]) => (result = r));
    const req = httpMock.expectOne(`${base}/expense-categories`);
    expect(req.request.method).toBe('GET');
    req.flush({ rows: [{ name: 'Groceries' }, { name: 'Transport' }] });
    expect(result).toEqual(['Groceries', 'Transport']);
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
