import { provideZonelessChangeDetection } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { formatArs } from '../../../../core/money/money';
import { Money } from '../../../../core/types/money';
import { CreditorPayableRow } from '../../types/creditor-payable-row';
import { CreditorPayablesTable } from './creditor-payables-table';

describe('CreditorPayablesTable', () => {
  let fixture: ComponentFixture<CreditorPayablesTable>;

  const money = (value: number): Money => value as Money;
  const rows: CreditorPayableRow[] = [{
    creditorId: 'cred-1',
    creditorName: 'Bank A',
    dueNowMinorUnits: money(300000),
    totalOwedMinorUnits: money(500000),
    nextDueDate: '2026-10-01',
    accounts: [{
      accountId: 'acc-1',
      label: 'Savings',
      outstandingMinorUnits: money(500000)
    }]
  }, {
    creditorId: 'cred-2',
    creditorName: 'Bank B',
    dueNowMinorUnits: money(750000),
    totalOwedMinorUnits: money(900000),
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
      providers: [provideZonelessChangeDetection(), provideRouter([])]
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
  it('renders both the due-now and total-owed figures for a row', () => {
    fixture.componentRef.setInput('payables', rows);
    fixture.detectChanges();
    const rowEls: NodeListOf<HTMLElement> = fixture.nativeElement.querySelectorAll('tbody tr.payable-row');
    expect(rowEls[0].textContent).toContain(formatArs(money(300000)));
    expect(rowEls[0].textContent).toContain(formatArs(money(500000)));
    expect(rowEls[0].textContent).toContain('total');
  });
  it('shows one tr.payable-row per creditor', () => {
    fixture.componentRef.setInput('payables', rows);
    fixture.detectChanges();
    const rowEls: NodeListOf<HTMLElement> = fixture.nativeElement.querySelectorAll('tbody tr.payable-row');
    expect(rowEls.length).toBe(2);
  });
  it('links each creditor name to its detail route', () => {
    fixture.componentRef.setInput('payables', rows);
    fixture.detectChanges();
    const link: HTMLAnchorElement = fixture.nativeElement.querySelector('tbody tr.payable-row a');
    expect(link.getAttribute('href')).toBe('/financing/creditor-payables/cred-1');
  });
  it('shows an empty note when there are no creditors', () => {
    fixture.componentRef.setInput('payables', []);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain("You don't owe any creditors.");
    expect(fixture.nativeElement.querySelector('table')).toBeNull();
  });
});
