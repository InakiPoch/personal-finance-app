import { provideZonelessChangeDetection } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Money } from '../../../../core/types/money';
import { CreditorPayableRow } from '../../types/creditor-payable-row';
import { CreditorPayablesTable } from './creditor-payables-table';

describe('CreditorPayablesTable', () => {
  let fixture: ComponentFixture<CreditorPayablesTable>;

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
  }, {
    creditorId: 'cred-2',
    creditorName: 'Bank B',
    outstandingMinorUnits: money(750000),
    nextDueDate: null,
    accounts: [{
      accountId: 'acc-2',
      label: 'Checking',
      outstandingMinorUnits: money(750000)
    }]
  }];

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [CreditorPayablesTable],
      providers: [provideZonelessChangeDetection()]
    });
    fixture = TestBed.createComponent(CreditorPayablesTable);
  });

  it('renders each creditor with its name and amount', () => {
    fixture.componentRef.setInput('payables', rows);
    fixture.detectChanges();
    const text: string = fixture.nativeElement.textContent;
    expect(text).toContain('Bank A');
    expect(text).toContain('Bank B');
    expect(text).toContain('$');
  });
  it('shows one tr.payable-row per creditor', () => {
    fixture.componentRef.setInput('payables', rows);
    fixture.detectChanges();
    const rowEls: NodeListOf<HTMLElement> = fixture.nativeElement.querySelectorAll('tbody tr.payable-row');
    expect(rowEls.length).toBe(2);
  });
  it('shows an empty note when there are no creditors', () => {
    fixture.componentRef.setInput('payables', []);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain("You don't owe any creditors.");
    expect(fixture.nativeElement.querySelector('table')).toBeNull();
  });
});
