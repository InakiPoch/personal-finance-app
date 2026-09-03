import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { ActiveSubscription } from './types/active-subscription';
import { CreateSubscription } from './types/create-subscription';
import { Frequency } from './types/frequency';
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
            frequency: row.frequency.toLowerCase() as Frequency,
          })),
        ),
      );
  }

  create(body: CreateSubscription): Observable<SubscriptionResult> {
    return this.http.post<SubscriptionResult>('subscriptions', body);
  }

  cancel(id: string): Observable<void> {
    return this.http.delete<void>(`subscriptions/${id}`);
  }
}
