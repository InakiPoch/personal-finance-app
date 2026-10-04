import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { ActiveSubscription } from './types/active-subscription';
import { CreateSubscription } from './types/create-subscription';
import { Frequency } from './types/frequency';
import { MonthSubscription } from './types/month-subscription';
import { PaySubscriptionResult } from './types/pay-subscription-result';
import { SubscriptionResult } from './types/subscription-result';

type RowsEnvelope<T> = { rows: T[] };

@Injectable({ providedIn: 'root' })
export class SubscriptionsService {
  private readonly http: HttpClient = inject(HttpClient);

  listActive(): Observable<ActiveSubscription[]> {
    return this.http
      .get<RowsEnvelope<ActiveSubscription>>('subscriptions/active')
    .pipe(
      map((envelope: RowsEnvelope<ActiveSubscription>) =>
        envelope.rows.map((row: ActiveSubscription) => ({
          ...row,
          frequency: row.frequency.toLowerCase() as Frequency
        }))
      )
    );
  }

  listByMonth(month: string): Observable<MonthSubscription[]> {
    return this.http
      .get<RowsEnvelope<MonthSubscription>>('subscriptions/by-month', { params: { month } })
    .pipe(
      map((envelope: RowsEnvelope<MonthSubscription>) =>
        envelope.rows.map((row: MonthSubscription) => ({
          ...row,
          frequency: row.frequency.toLowerCase() as Frequency
        }))
      )
    );
  }

  create(body: CreateSubscription): Observable<SubscriptionResult> {
    return this.http.post<SubscriptionResult>('subscriptions', body);
  }

  cancel(id: string): Observable<void> {
    return this.http.delete<void>(`subscriptions/${id}`);
  }

  pay(id: string): Observable<PaySubscriptionResult> {
    return this.http.post<PaySubscriptionResult>(`subscriptions/${id}/pay`, {});
  }

  unpay(id: string): Observable<PaySubscriptionResult> {
    return this.http.post<PaySubscriptionResult>(`subscriptions/${id}/unpay`, {});
  }
}
