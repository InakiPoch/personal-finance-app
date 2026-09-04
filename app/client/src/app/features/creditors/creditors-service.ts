import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { CreateCreditor } from './types/create-creditor';
import { Creditor } from './types/creditor';
import { CreditorCreated } from './types/creditor-created';

type RowsEnvelope<T> = { rows: T[] };

@Injectable({ providedIn: 'root' })
export class CreditorsService {
  private readonly http: HttpClient = inject(HttpClient);

  list(): Observable<Creditor[]> {
    return this.http
      .get<RowsEnvelope<Creditor>>('creditors')
      .pipe(map((envelope: RowsEnvelope<Creditor>) => envelope.rows));
  }

  create(body: CreateCreditor): Observable<CreditorCreated> {
    return this.http.post<CreditorCreated>('creditors', body);
  }
}
