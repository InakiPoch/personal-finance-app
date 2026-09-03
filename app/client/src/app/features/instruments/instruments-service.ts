import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { CreateInstrument } from './types/create-instrument';
import { InstrumentCreated } from './types/instrument-created';

@Injectable({ providedIn: 'root' })
export class InstrumentsService {
  private readonly http: HttpClient = inject(HttpClient);

  create(body: CreateInstrument): Observable<InstrumentCreated> {
    return this.http.post<InstrumentCreated>('instruments', body);
  }
}
