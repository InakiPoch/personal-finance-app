import { provideZonelessChangeDetection } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Observable, of, throwError } from 'rxjs';
import { Money } from '../../../../core/types/money';
import { FinancingService } from '../../financing-service';
import { CreditorPayableRow } from '../../types/creditor-payable-row';
import { CreditorPayablesPage } from './creditor-payables-page';

type CreditorPayablesView = {
  payables: () => CreditorPayableRow[];
  loadStatus: () => 'idle' | 'loading' | 'ready' | 'error';
};

describe('CreditorPayablesPage', () => {
  let fixture: ComponentFixture<CreditorPayablesPage>;
  let view: CreditorPayablesView;
  let creditorPayables: jasmine.Spy<() => Observable<CreditorPayableRow[]>>;

  const money = (value: number): Money => value as Money;

  const rows: CreditorPayableRow[] = [{
    creditorId: 'cred-1',
    creditorName: 'Bank A',
    outstandingMinorUnits: money(500000),
    nextDueDate: '2026-10-01',
    accounts: [{
      accountId: 'acc-1',
      label: 'Savings',
      outstandingMinorUnits: money(500000)
    }]
  }];

  function setup(): void {
    TestBed.configureTestingModule({
      imports: [CreditorPayablesPage],
      providers: [
        provideZonelessChangeDetection(),
        { provide: FinancingService, useValue: { creditorPayables } }
      ]
    });
    fixture = TestBed.createComponent(CreditorPayablesPage);
    view = fixture.componentInstance as unknown as CreditorPayablesView;
    fixture.detectChanges();
  }
  
  it('fetches and renders the payables on init', () => {
    creditorPayables = jasmine.createSpy('creditorPayables').and.returnValue(of(rows));
    setup();
    expect(creditorPayables).toHaveBeenCalled();
    expect(view.loadStatus()).toBe('ready');
    expect(view.payables()).toEqual(rows);
  });
  it('renders the empty state when there are no creditors', () => {
    creditorPayables = jasmine.createSpy('creditorPayables').and.returnValue(of([]));
    setup();
    expect(view.loadStatus()).toBe('ready');
    const text: string = fixture.nativeElement.textContent;
    expect(text).toContain("You don't owe any creditors.");
  });
  it('surfaces a load error without throwing', () => {
    creditorPayables = jasmine.createSpy('creditorPayables').and.returnValue(throwError(() => new Error('boom')));
    setup();
    expect(view.loadStatus()).toBe('error');
    expect(view.payables()).toEqual([]);
  });
});
