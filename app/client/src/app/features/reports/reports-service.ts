import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { CardDueRow } from './types/card-due-row';
import { MonthlyExpenseRow } from './types/monthly-expense-row';

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

  cardDueByMonth(): Observable<CardDueRow[]> {
    return this.http.get<RowsEnvelope<CardDueRow>>('reports/card-due-by-month').pipe(map((envelope: RowsEnvelope<CardDueRow>) => envelope.rows));
  }
}
