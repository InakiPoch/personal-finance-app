import { provideZonelessChangeDetection } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { formatMoney } from '../../../../core/money/money';
import { Money } from '../../../../core/types/money';
import { RecentPurchaseRow } from '../../types/recent-purchase-row';
import { RecentPurchasesTable } from './recent-purchases-table';

describe('RecentPurchasesTable', () => {
  let fixture: ComponentFixture<RecentPurchasesTable>;

  const money = (value: number): Money => value as Money;
  const rows: RecentPurchaseRow[] = [{
      planId: 'plan-1',
      description: 'New laptop',
      cardName: 'Visa',
      purchaseDate: '2026-09-01',
      totalMinorUnits: money(1200000),
      installmentCount: 3,
      isCreditorPayment: false,
      paidInstallmentCount: 1,
      nextDueYear: 2026,
      nextDueMonth: 11,
      pendingAmountMinorUnits: money(800000),
      currencyCode: 'ARS'
    },{
      planId: 'plan-2',
      description: 'Rent',
      cardName: 'Visa',
      purchaseDate: '2026-08-15',
      totalMinorUnits: money(500000),
      installmentCount: 1,
      isCreditorPayment: true,
      paidInstallmentCount: 1,
      nextDueYear: null,
      nextDueMonth: null,
      pendingAmountMinorUnits: money(0),
      currencyCode: 'ARS'
  }];

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [RecentPurchasesTable],
      providers: [provideZonelessChangeDetection()]
    });
    fixture = TestBed.createComponent(RecentPurchasesTable);
  });

  it('renders each purchase with its description, card name and amount', () => {
    fixture.componentRef.setInput('purchases', rows);
    fixture.detectChanges();
    const text: string = fixture.nativeElement.textContent;
    expect(text).toContain('New laptop');
    expect(text).toContain('Rent');
    expect(text).toContain('Visa');
    expect(text).toContain('$');
  });
  it('flags a creditor payment and no others', () => {
    fixture.componentRef.setInput('purchases', rows);
    fixture.detectChanges();
    const rowEls: NodeListOf<HTMLElement> = fixture.nativeElement.querySelectorAll('tbody tr');
    expect(rowEls[0].textContent).not.toContain('Creditor payment');
    expect(rowEls[1].textContent).toContain('Creditor payment');
  });
  it('renders the paid-of-total installment count', () => {
    fixture.componentRef.setInput('purchases', rows);
    fixture.detectChanges();
    const rowEls: NodeListOf<HTMLElement> = fixture.nativeElement.querySelectorAll('tbody tr');
    expect(rowEls[0].textContent).toContain('1/3 paid');
  });
  it('renders the next payment month when one is due, and "Fully paid" when none remains', () => {
    fixture.componentRef.setInput('purchases', rows);
    fixture.detectChanges();
    const rowEls: NodeListOf<HTMLElement> = fixture.nativeElement.querySelectorAll('tbody tr');
    expect(rowEls[0].textContent).toContain('next: Nov 2026');
    expect(rowEls[1].textContent).toContain('Fully paid');
  });
  it('renders the pending amount for a partly-paid row and hides it for a fully-paid one', () => {
    fixture.componentRef.setInput('purchases', rows);
    fixture.detectChanges();
    const rowEls: NodeListOf<HTMLElement> = fixture.nativeElement.querySelectorAll('tbody tr');
    expect(rowEls[0].textContent).toContain('pending');
    expect(rowEls[1].textContent).not.toContain('pending');
  });
  it('shows an empty note when there are no purchases', () => {
    fixture.componentRef.setInput('purchases', []);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('No purchases loaded yet.');
    expect(fixture.nativeElement.querySelector('table')).toBeNull();
  });
  it('renders a USD row amount in dollar formatting, not peso formatting', () => {
    const usdRow: RecentPurchaseRow = { ...rows[0], planId: 'plan-3', currencyCode: 'USD' };
    fixture.componentRef.setInput('purchases', [usdRow]);
    fixture.detectChanges();
    const text: string = fixture.nativeElement.textContent;
    expect(text).toContain(formatMoney(usdRow.totalMinorUnits, 'USD'));
    expect(text).not.toContain(formatMoney(usdRow.totalMinorUnits, 'ARS'));
  });
});
