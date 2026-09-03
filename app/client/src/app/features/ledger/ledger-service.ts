import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { AccountBalance } from './types/account-balance';
import { PostTransaction } from './types/post-transaction';
import { PostTransactionResult } from './types/post-transaction-result';
import { ReverseTransactionResult } from './types/reverse-transaction-result';

@Injectable({ providedIn: 'root' })
export class LedgerService {
  private readonly http: HttpClient = inject(HttpClient);

  postTransaction(body: PostTransaction): Observable<PostTransactionResult> {
    return this.http.post<PostTransactionResult>('ledger/transactions', body);
  }

  reverse(transactionId: string): Observable<ReverseTransactionResult> {
    return this.http.post<ReverseTransactionResult>(
      `ledger/transactions/${transactionId}/reversal`,
      {},
    );
  }

  getAccountBalance(accountId: string): Observable<AccountBalance> {
    return this.http.get<AccountBalance>(`ledger/accounts/${accountId}/balance`);
  }
}
