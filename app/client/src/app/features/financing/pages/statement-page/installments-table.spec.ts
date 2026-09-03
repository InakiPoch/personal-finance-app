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
    },{
      planId: 'pl1',
      installmentId: 'i2',
      sequence: 2,
      installmentCount: 3,
      purchaseDate: '2026-09-01',
      cycleYear: 2026,
      cycleMonth: 10,
      amountMinorUnits: money(100000),
      isReversed: true,
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
  it('shows an empty note when there are no installments', () => {
    fixture.componentRef.setInput('installments', []);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.installments__empty')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('table')).toBeNull();
  });
});
