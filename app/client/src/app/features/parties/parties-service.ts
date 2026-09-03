import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { CurrentAccountBalance } from './types/current-account-balance';

@Injectable({ providedIn: 'root' })
export class PartiesService {
  private readonly http: HttpClient = inject(HttpClient);

  getBalance(partyId: string): Observable<CurrentAccountBalance> {
    return this.http.get<CurrentAccountBalance>(`parties/${partyId}/balance`);
  }
}
