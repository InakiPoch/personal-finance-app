import { provideZonelessChangeDetection } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { InstrumentRegistryService } from './instrument-registry-service';
import { RegisteredInstrument } from '../types/registered-instrument';

const STORAGE_KEY: string = 'pf.instrument-registry';

function instrument(overrides: Partial<RegisteredInstrument> = {}): RegisteredInstrument {
  return { id: 'i1', type: 'debit', name: 'Checking', ...overrides };
}

function makeService(): InstrumentRegistryService {
  TestBed.configureTestingModule({ providers: [provideZonelessChangeDetection()] });
  return TestBed.inject(InstrumentRegistryService);
}

describe('InstrumentRegistryService', () => {
  beforeEach(() => localStorage.clear());
  afterEach(() => localStorage.clear());

  it('starts empty when nothing is stored', () => {
    expect(makeService().instruments()).toEqual([]);
  });
  it('add() appends to the signal and persists to localStorage', () => {
    const service: InstrumentRegistryService = makeService();
    service.add(instrument({ id: 'i1' }));
    service.add(instrument({ id: 'i2', type: 'credit', name: 'Visa', cutoffDate: 20 }));
    expect(service.instruments().map((i: RegisteredInstrument) => i.id)).toEqual(['i1', 'i2']);
    expect(JSON.parse(localStorage.getItem(STORAGE_KEY) ?? '[]')).toEqual([
      instrument({ id: 'i1' }),
      instrument({ id: 'i2', type: 'credit', name: 'Visa', cutoffDate: 20 }),
    ]);
  });
  it('hydrates the signal from existing localStorage content', () => {
    localStorage.setItem(STORAGE_KEY, JSON.stringify([instrument({ id: 'seed' })]));
    expect(makeService().instruments().map((i: RegisteredInstrument) => i.id)).toEqual(['seed']);
  });
  it('falls back to empty on corrupt JSON without throwing', () => {
    localStorage.setItem(STORAGE_KEY, '{not json');
    let service: InstrumentRegistryService | undefined;
    expect(() => (service = makeService())).not.toThrow();
    expect(service?.instruments()).toEqual([]);
  });
  it('falls back to empty when the stored content is not an array', () => {
    localStorage.setItem(STORAGE_KEY, JSON.stringify({ id: 'x' }));
    expect(makeService().instruments()).toEqual([]);
  });
  it('swallows a setItem failure (quota / privacy mode)', () => {
    const service: InstrumentRegistryService = makeService();
    spyOn(Storage.prototype, 'setItem').and.throwError('QuotaExceededError');
    expect(() => service.add(instrument())).not.toThrow();
    expect(service.instruments().map((i: RegisteredInstrument) => i.id)).toEqual(['i1']);
  });
});
