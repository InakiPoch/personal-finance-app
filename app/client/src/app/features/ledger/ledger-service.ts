import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { AccountBalance } from './types/account-balance';
import { PostTransaction } from './types/post-transaction';
import { PostTransactionResult } from './types/post-transaction-result';
import { RecordDebitExpense } from './types/record-debit-expense';
import { RecordDebitExpenseResult } from './types/record-debit-expense-result';
import { RecordIncome } from './types/record-income';
import { RecordIncomeResult } from './types/record-income-result';
import { ReverseTransactionResult } from './types/reverse-transaction-result';

type RowsEnvelope<T> = { rows: T[] };

@Injectable({ providedIn: 'root' })
export class LedgerService {
  private readonly http: HttpClient = inject(HttpClient);

  postTransaction(body: PostTransaction): Observable<PostTransactionResult> {
    return this.http.post<PostTransactionResult>('ledger/transactions', body);
  }

  recordDebitExpense(body: RecordDebitExpense): Observable<RecordDebitExpenseResult> {
    return this.http.post<RecordDebitExpenseResult>('ledger/expenses', body);
  }

  recordIncome(body: RecordIncome): Observable<RecordIncomeResult> {
    return this.http.post<RecordIncomeResult>('ledger/incomes', body);
  }

  listExpenseCategories(): Observable<string[]> {
    return this.http
      .get<RowsEnvelope<{ name: string }>>('expense-categories')
    .pipe(map((envelope: RowsEnvelope<{ name: string }>) => envelope.rows.map((row) => row.name)));
  }

  reverse(transactionId: string): Observable<ReverseTransactionResult> {
    return this.http.post<ReverseTransactionResult>(`ledger/transactions/${transactionId}/reversal`, {});
  }

  getAccountBalance(accountId: string): Observable<AccountBalance> {
    return this.http.get<AccountBalance>(`ledger/accounts/${accountId}/balance`);
  }
}
