import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { IsoDate } from '../../core/types/iso-date';
import { AccountBalance } from './types/account-balance';
import { PostTransaction } from './types/post-transaction';
import { PostTransactionResult } from './types/post-transaction-result';
import { RecordDebitExpense } from './types/record-debit-expense';
import { RecordDebitExpenseResult } from './types/record-debit-expense-result';
import { ReverseTransactionResult } from './types/reverse-transaction-result';
import { TransactionRow } from './types/transaction-row';

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

  listExpenseCategories(): Observable<string[]> {
    return this.http
      .get<RowsEnvelope<{ name: string }>>('expense-categories')
    .pipe(map((envelope: RowsEnvelope<{ name: string }>) => envelope.rows.map((row) => row.name)));
  }

  listTransactions(
    filter: { accountId?: string; from?: IsoDate; to?: IsoDate } = {},
  ): Observable<TransactionRow[]> {
    let params: HttpParams = new HttpParams();
    if(filter.accountId) {
      params = params.set('accountId', filter.accountId);
    }
    if(filter.from) {
      params = params.set('from', filter.from);
    }
    if(filter.to) {
      params = params.set('to', filter.to);
    }
    return this.http
      .get<RowsEnvelope<TransactionRow>>('ledger/transactions', { params })
    .pipe(map((envelope: RowsEnvelope<TransactionRow>) => envelope.rows));
  }

  reverse(transactionId: string): Observable<ReverseTransactionResult> {
    return this.http.post<ReverseTransactionResult>(`ledger/transactions/${transactionId}/reversal`, {});
  }

  getAccountBalance(accountId: string): Observable<AccountBalance> {
    return this.http.get<AccountBalance>(`ledger/accounts/${accountId}/balance`);
  }
}
