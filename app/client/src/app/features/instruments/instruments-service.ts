import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { ClosingDay } from './types/closing-day';
import { ClosingScheduleRow } from './types/closing-schedule-row';
import { CreateInstrument } from './types/create-instrument';
import { Instrument } from './types/instrument';
import { InstrumentCreated } from './types/instrument-created';

type RowsEnvelope<T> = { rows: T[] };

@Injectable({ providedIn: 'root' })
export class InstrumentsService {
  private readonly http: HttpClient = inject(HttpClient);

  list(): Observable<Instrument[]> {
    return this.http
      .get<RowsEnvelope<Instrument>>('instruments')
      .pipe(map((envelope: RowsEnvelope<Instrument>) => envelope.rows));
  }

  create(body: CreateInstrument): Observable<InstrumentCreated> {
    return this.http.post<InstrumentCreated>('instruments', body);
  }

  changeUsualClosingDay(id: string, day: number): Observable<void> {
    const body: ClosingDay = { day };
    return this.http.put<void>(`instruments/cards/${id}/closing-day`, body);
  }

  closingSchedule(id: string): Observable<ClosingScheduleRow[]> {
    return this.http
      .get<RowsEnvelope<ClosingScheduleRow>>(`instruments/cards/${id}/closing-dates`)
    .pipe(map((envelope: RowsEnvelope<ClosingScheduleRow>) => envelope.rows));
  }

  setClosingDate(id: string, year: number, month: number, day: number): Observable<void> {
    const body: ClosingDay = { day };
    return this.http.put<void>(`instruments/cards/${id}/closing-dates/${year}/${month}`, body);
  }

  clearClosingDate(id: string, year: number, month: number): Observable<void> {
    return this.http.delete<void>(`instruments/cards/${id}/closing-dates/${year}/${month}`);
  }
}
