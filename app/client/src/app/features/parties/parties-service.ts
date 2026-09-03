import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { CreateParty } from './types/create-party';
import { CurrentAccountBalance } from './types/current-account-balance';
import { CurrentAccountTimelineRow } from './types/current-account-timeline-row';
import { PartyResult } from './types/party-result';
import { RegisterSharedExpense } from './types/register-shared-expense';
import { SettleCurrentAccount } from './types/settle-current-account';
import { SettlementResult } from './types/settlement-result';
import { SharedExpenseResult } from './types/shared-expense-result';

type RowsEnvelope<T> = { rows: T[] };

@Injectable({ providedIn: 'root' })
export class PartiesService {
  private readonly http: HttpClient = inject(HttpClient);

  getBalance(partyId: string): Observable<CurrentAccountBalance> {
    return this.http.get<CurrentAccountBalance>(`parties/${partyId}/balance`);
  }

  getTimeline(partyId: string): Observable<CurrentAccountTimelineRow[]> {
    return this.http
      .get<RowsEnvelope<CurrentAccountTimelineRow>>(`parties/${partyId}/timeline`)
      .pipe(map((envelope: RowsEnvelope<CurrentAccountTimelineRow>) => envelope.rows));
  }

  create(body: CreateParty): Observable<PartyResult> {
    return this.http.post<PartyResult>('parties', body);
  }

  registerSharedExpense(body: RegisterSharedExpense): Observable<SharedExpenseResult> {
    return this.http.post<SharedExpenseResult>('parties/shared-expenses', body);
  }

  settle(partyId: string, body: SettleCurrentAccount): Observable<SettlementResult> {
    return this.http.post<SettlementResult>(`parties/${partyId}/settlements`, body);
  }
}
