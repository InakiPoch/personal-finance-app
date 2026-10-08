import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { IsoDate } from '../../core/types/iso-date';
import { MoneyFlowRow } from '../ledger/types/money-flow-row';
import { CardDueRow } from './types/card-due-row';
import { MonthlyExpenseRow } from './types/monthly-expense-row';
import { MonthlyIncomeRow } from './types/monthly-income-row';
import { OwedToYouRow } from './types/owed-to-you-row';
import { PartyDebtRow } from './types/party-debt-row';
import { PartyTimelineRow } from './types/party-timeline-row';
import { TransactionFeedRow } from './types/transaction-feed-row';

type RowsEnvelope<T> = { rows: T[] };

@Injectable({ providedIn: 'root' })
export class ReportsService {
  private readonly http: HttpClient = inject(HttpClient);

  monthlyExpenses(month?: string): Observable<MonthlyExpenseRow[]> {
    let params: HttpParams = new HttpParams();
    if(month !== undefined) {
      params = params.set('month', month);
    }
    return this.http.get<RowsEnvelope<MonthlyExpenseRow>>('reports/monthly-expenses', { params }).pipe(map((envelope: RowsEnvelope<MonthlyExpenseRow>) => envelope.rows));
  }

  monthlyIncomes(month?: string): Observable<MonthlyIncomeRow[]> {
    let params: HttpParams = new HttpParams();
    if(month !== undefined) {
      params = params.set('month', month);
    }
    return this.http.get<RowsEnvelope<MonthlyIncomeRow>>('reports/monthly-incomes', { params }).pipe(map((envelope: RowsEnvelope<MonthlyIncomeRow>) => envelope.rows));
  }

  moneyFlow(month: string): Observable<MoneyFlowRow[]> {
    const params: HttpParams = new HttpParams().set('month', month);
    return this.http.get<RowsEnvelope<MoneyFlowRow>>('reports/money-flow', { params }).pipe(map((envelope: RowsEnvelope<MoneyFlowRow>) => envelope.rows));
  }

  cardDueByMonth(): Observable<CardDueRow[]> {
    return this.http.get<RowsEnvelope<CardDueRow>>('reports/card-due-by-month').pipe(map((envelope: RowsEnvelope<CardDueRow>) => envelope.rows));
  }

  owedToYou(month: string, today: string): Observable<OwedToYouRow[]> {
    return this.http
      .get<RowsEnvelope<OwedToYouRow>>('reports/parties/owed-to-you', { params: { month, today } })
    .pipe(map((envelope: RowsEnvelope<OwedToYouRow>) => envelope.rows));
  }

  debtSummary(): Observable<PartyDebtRow[]> {
    return this.http
      .get<RowsEnvelope<PartyDebtRow>>('reports/parties/debt-summary')
    .pipe(map((envelope: RowsEnvelope<PartyDebtRow>) => envelope.rows));
  }

  partyTimeline(partyId: string): Observable<PartyTimelineRow[]> {
    return this.http
      .get<RowsEnvelope<PartyTimelineRow>>(`reports/parties/${partyId}/timeline`)
    .pipe(map((envelope: RowsEnvelope<PartyTimelineRow>) => envelope.rows));
  }

  transactions(
    filter: { accountId?: string; from?: IsoDate; to?: IsoDate } = {},
  ): Observable<TransactionFeedRow[]> {
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
      .get<RowsEnvelope<TransactionFeedRow>>('reports/transactions', { params })
      .pipe(map((envelope: RowsEnvelope<TransactionFeedRow>) => envelope.rows));
  }

  transaction(id: string): Observable<TransactionFeedRow> {
    return this.http.get<TransactionFeedRow>(`reports/transactions/${id}`);
  }
}
