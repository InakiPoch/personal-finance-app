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
      reversalTransactionId: 'tx-1'
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
      reversalTransactionId: 'tx-2'
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
      reversalTransactionId: null
  }];

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
    const buttons: NodeListOf<HTMLButtonElement> =
      fixture.nativeElement.querySelectorAll('tbody tr button');
    expect(buttons[0].disabled).toBeFalse();
    expect(buttons[1].disabled).toBeTrue();
    expect(buttons[2].disabled).toBeTrue();
  });
  it('emits the accrual transaction id when an active Reverse button is clicked', () => {
    fixture.componentRef.setInput('installments', rows);
    let emitted: string | undefined;
    fixture.componentInstance.reverseClick.subscribe((id: string) => (emitted = id));
    fixture.detectChanges();
    const button: HTMLButtonElement = fixture.nativeElement.querySelector('tbody tr button');
    button.click();
    expect(emitted).toBe('tx-1');
  });
  it('shows an empty note when there are no installments', () => {
    fixture.componentRef.setInput('installments', []);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.installments__empty')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('table')).toBeNull();
  });
});
