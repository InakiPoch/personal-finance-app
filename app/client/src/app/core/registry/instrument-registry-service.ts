import { Injectable, Signal, WritableSignal, signal } from '@angular/core';
import { RegisteredInstrument } from '../types/registered-instrument';

@Injectable({ providedIn: 'root' })
export class InstrumentRegistryService {
  private readonly storageKey: string = 'pf.instrument-registry';
  private readonly instrumentsSignal: WritableSignal<RegisteredInstrument[]> = signal(this.read());
  readonly instruments: Signal<RegisteredInstrument[]> = this.instrumentsSignal.asReadonly();

  add(instrument: RegisteredInstrument): void {
    this.instrumentsSignal.update((list: RegisteredInstrument[]) => [...list, instrument]);
    this.persist(this.instrumentsSignal());
  }

  private read(): RegisteredInstrument[] {
    if(typeof localStorage === 'undefined') {
      return [];
    }
    try {
      const raw: string | null = localStorage.getItem(this.storageKey);
      const parsed: unknown = raw === null ? [] : JSON.parse(raw);
      return Array.isArray(parsed) ? (parsed as RegisteredInstrument[]) : [];
    } catch {
      return [];
    }
  }

  private persist(list: RegisteredInstrument[]): void {
    if(typeof localStorage === 'undefined') {
      return;
    }
    try {
      localStorage.setItem(this.storageKey, JSON.stringify(list));
    } catch {
      //
    }
  }
}
