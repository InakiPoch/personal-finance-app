import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { BorrowingResult } from './types/borrowing-result';
import { CreateParty } from './types/create-party';
import { CurrentAccountBalance } from './types/current-account-balance';
import { CurrentAccountTimelineRow } from './types/current-account-timeline-row';
import { FuturePartyShare } from './types/future-party-share';
import { LoanResult } from './types/loan-result';
import { Party } from './types/party';
import { PartyResult } from './types/party-result';
import { PartyPurchaseResult } from './types/party-purchase-result';
import { PendingSharesByPartyRow } from './types/pending-shares-by-party-row';
import { RecordBorrowing } from './types/record-borrowing';
import { RecordLoan } from './types/record-loan';
import { RecordPartyPurchase } from './types/record-party-purchase';
import { RecordRepayment } from './types/record-repayment';
import { RepaymentResult } from './types/repayment-result';
import { SettleCurrentAccount } from './types/settle-current-account';
import { SettlementResult } from './types/settlement-result';

type RowsEnvelope<T> = { rows: T[] };

@Injectable({ providedIn: 'root' })
export class PartiesService {
  private readonly http: HttpClient = inject(HttpClient);

  list(): Observable<Party[]> {
    return this.http
      .get<RowsEnvelope<Party>>('parties')
    .pipe(map((envelope: RowsEnvelope<Party>) => envelope.rows));
  }

  getBalance(partyId: string): Observable<CurrentAccountBalance> {
    return this.http.get<CurrentAccountBalance>(`parties/${partyId}/balance`);
  }

  getTimeline(partyId: string): Observable<CurrentAccountTimelineRow[]> {
    return this.http
      .get<RowsEnvelope<CurrentAccountTimelineRow>>(`parties/${partyId}/timeline`)
    .pipe(map((envelope: RowsEnvelope<CurrentAccountTimelineRow>) => envelope.rows));
  }

  futureShares(partyId: string): Observable<FuturePartyShare[]> {
    return this.http
      .get<RowsEnvelope<FuturePartyShare>>(`parties/${partyId}/future-shares`)
    .pipe(map((envelope: RowsEnvelope<FuturePartyShare>) => envelope.rows));
  }

  pendingShares(): Observable<PendingSharesByPartyRow[]> {
    return this.http
      .get<RowsEnvelope<PendingSharesByPartyRow>>('parties/pending-shares')
    .pipe(map((envelope: RowsEnvelope<PendingSharesByPartyRow>) => envelope.rows));
  }

  create(body: CreateParty): Observable<PartyResult> {
    return this.http.post<PartyResult>('parties', body);
  }

  recordLoan(partyId: string, body: RecordLoan): Observable<LoanResult> {
    return this.http.post<LoanResult>(`parties/${partyId}/loans`, body);
  }

  recordBorrowing(partyId: string, body: RecordBorrowing): Observable<BorrowingResult> {
    return this.http.post<BorrowingResult>(`parties/${partyId}/borrowings`, body);
  }

  recordPartyPurchase(partyId: string, body: RecordPartyPurchase): Observable<PartyPurchaseResult> {
    return this.http.post<PartyPurchaseResult>(`parties/${partyId}/purchases`, body);
  }

  undoPartyPurchase(partyId: string, purchaseId: string): Observable<void> {
    return this.http.post<void>(`parties/${partyId}/purchases/${purchaseId}/undo`, null);
  }

  repay(partyId: string, body: RecordRepayment): Observable<RepaymentResult> {
    return this.http.post<RepaymentResult>(`parties/${partyId}/repayments`, body);
  }

  settle(partyId: string, body: SettleCurrentAccount): Observable<SettlementResult> {
    return this.http.post<SettlementResult>(`parties/${partyId}/settlements`, body);
  }
}
