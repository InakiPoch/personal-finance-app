import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
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
}
