import { provideZonelessChangeDetection } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Money } from '../../../../core/types/money';
import { MonthlyStatementInstallment } from '../../types/monthly-statement-installment';
import { InstallmentsTable } from './installments-table';

describe('InstallmentsTable', () => {
  let fixture: ComponentFixture<InstallmentsTable>;

  const money = (value: number): Money => value as Money;
  const rows: MonthlyStatementInstallment[] = [{
      planId: 'pl1',
      installmentId: 'i1',
      sequence: 1,
      installmentCount: 3,
      purchaseDate: '2026-09-01',
      cycleYear: 2026,
      cycleMonth: 9,
      amountMinorUnits: money(100000),
      isReversed: false,
      reversalTransactionId: 'tx-1',
      isPaid: false,
      paidOnUtc: null,
      currencyCode: 'ARS'
    }, {
      planId: 'pl1',
      installmentId: 'i2',
      sequence: 2,
      installmentCount: 3,
      purchaseDate: '2026-09-01',
      cycleYear: 2026,
      cycleMonth: 10,
      amountMinorUnits: money(100000),
      isReversed: true,
      reversalTransactionId: 'tx-2',
      isPaid: false,
      paidOnUtc: null,
      currencyCode: 'ARS'
    }, {
      planId: 'pl1',
      installmentId: 'i3',
      sequence: 3,
      installmentCount: 3,
      purchaseDate: '2026-09-01',
      cycleYear: 2026,
      cycleMonth: 11,
      amountMinorUnits: money(100000),
      isReversed: false,
      reversalTransactionId: null,
      isPaid: false,
      paidOnUtc: null,
      currencyCode: 'ARS'
  }];

  const buttonsByLabel = (label: string): HTMLButtonElement[] => {
    const nodes: NodeListOf<HTMLButtonElement> =
      fixture.nativeElement.querySelectorAll('tbody tr button');
    return Array.from(nodes).filter(
      (button: HTMLButtonElement) => button.textContent?.trim() === label
    );
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [InstallmentsTable],
      providers: [provideZonelessChangeDetection()],
    });
    fixture = TestBed.createComponent(InstallmentsTable);
  });

  it('renders each installment as "N of M" with its amount', () => {
    fixture.componentRef.setInput('installments', rows);
    fixture.detectChanges();
    const text: string = fixture.nativeElement.textContent;
    expect(text).toContain('1 of 3');
    expect(text).toContain('2 of 3');
    expect(text).toContain('$');
  });
  it('flags a reversed installment and no others', () => {
    fixture.componentRef.setInput('installments', rows);
    fixture.detectChanges();
    const badges: NodeListOf<HTMLElement> =
      fixture.nativeElement.querySelectorAll('.installments__badge');
    expect(badges.length).toBe(1);
    expect(badges[0].textContent).toContain('Reversed');
  });
  it('enables Reverse only for a live, un-reversed accrual', () => {
    fixture.componentRef.setInput('installments', rows);
    fixture.detectChanges();
    const reverse: HTMLButtonElement[] = buttonsByLabel('Reverse');
    expect(reverse[0].disabled).toBeFalse();
    expect(reverse[1].disabled).toBeTrue();
    expect(reverse[2].disabled).toBeTrue();
  });
  it('emits the accrual transaction id when an active Reverse button is clicked', () => {
    fixture.componentRef.setInput('installments', rows);
    let emitted: string | undefined;
    fixture.componentInstance.reverseClick.subscribe((id: string) => (emitted = id));
    fixture.detectChanges();
    buttonsByLabel('Reverse')[0].click();
    expect(emitted).toBe('tx-1');
  });
  it('enables Pay only for an unpaid, un-reversed installment', () => {
    const mixed: MonthlyStatementInstallment[] = [
      rows[0],
      rows[1],
      { ...rows[2], isPaid: true, paidOnUtc: '2026-09-20T12:00:00Z' }
    ];
    fixture.componentRef.setInput('installments', mixed);
    fixture.detectChanges();
    const pay: HTMLButtonElement[] = buttonsByLabel('Pay');
    expect(pay[0].disabled).toBeFalse();
    expect(pay[1].disabled).toBeTrue();
    expect(pay[2].disabled).toBeTrue();
  });
  it('emits the installment id when an active Pay button is clicked', () => {
    fixture.componentRef.setInput('installments', rows);
    let emitted: string | undefined;
    fixture.componentInstance.payClick.subscribe((id: string) => (emitted = id));
    fixture.detectChanges();
    buttonsByLabel('Pay')[0].click();
    expect(emitted).toBe('i1');
  });
  it('disables every Pay button while a payment is in flight', () => {
    fixture.componentRef.setInput('installments', rows);
    fixture.componentRef.setInput('paying', true);
    fixture.detectChanges();
    const pay: HTMLButtonElement[] = buttonsByLabel('Pay');
    expect(pay.every((button: HTMLButtonElement) => button.disabled)).toBeTrue();
  });
  it('shows a Paid chip for a paid installment', () => {
    const mixed: MonthlyStatementInstallment[] = [
      { ...rows[0], isPaid: true, paidOnUtc: '2026-09-20T12:00:00Z' },
      rows[2]
    ];
    fixture.componentRef.setInput('installments', mixed);
    fixture.detectChanges();
    const badges: NodeListOf<HTMLElement> =
      fixture.nativeElement.querySelectorAll('.installments__badge');
    expect(badges.length).toBe(1);
    expect(badges[0].textContent).toContain('Paid');
  });
  it('shows an empty note when there are no installments', () => {
    fixture.componentRef.setInput('installments', []);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.installments__empty')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('table')).toBeNull();
  });
});
