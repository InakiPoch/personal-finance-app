import { provideZonelessChangeDetection } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
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
      isCreditorPayment: false
    },{
      planId: 'plan-2',
      description: 'Rent',
      cardName: 'Visa',
      purchaseDate: '2026-08-15',
      totalMinorUnits: money(500000),
      installmentCount: 1,
      isCreditorPayment: true
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
  it('shows an empty note when there are no purchases', () => {
    fixture.componentRef.setInput('purchases', []);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('No purchases loaded yet.');
    expect(fixture.nativeElement.querySelector('table')).toBeNull();
  });
});
