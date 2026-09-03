import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { CardFutureSchedule } from './types/card-future-schedule';
import { CreatePaymentPlan } from './types/create-payment-plan';
import { CreatePaymentPlanResult } from './types/create-payment-plan-result';
import { MonthlyStatement } from './types/monthly-statement';
import { MonthlyStatementSummary } from './types/monthly-statement-summary';
import { PayStatement } from './types/pay-statement';
import { PayStatementResult } from './types/pay-statement-result';

type RowsEnvelope<T> = { rows: T[] };

@Injectable({ providedIn: 'root' })
export class FinancingService {
  private readonly http: HttpClient = inject(HttpClient);

  createPaymentPlan(body: CreatePaymentPlan): Observable<CreatePaymentPlanResult> {
    return this.http.post<CreatePaymentPlanResult>('financing/payment-plans', body);
  }

  getStatement(id: string): Observable<MonthlyStatement> {
    return this.http.get<MonthlyStatement>(`financing/statements/${id}`);
  }

  payStatement(id: string, body: PayStatement): Observable<PayStatementResult> {
    return this.http.post<PayStatementResult>(`financing/statements/${id}/pay`, body);
  }

  getFutureSchedule(cardId: string): Observable<CardFutureSchedule> {
    return this.http.get<CardFutureSchedule>(`financing/cards/${cardId}/future-schedule`);
  }

  listStatements(cardId: string): Observable<MonthlyStatementSummary[]> {
    return this.http
      .get<RowsEnvelope<MonthlyStatementSummary>>(`financing/cards/${cardId}/statements`)
    .pipe(map((envelope: RowsEnvelope<MonthlyStatementSummary>) => envelope.rows));
  }
}
