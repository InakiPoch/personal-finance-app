import { provideZonelessChangeDetection } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { formatArs } from '../../../../core/money/money';
import { Money } from '../../../../core/types/money';
import { CreditorPurchaseGroup } from '../../types/creditor-purchase-group';
import { CreditorPurchasesTable } from './creditor-purchases-table';

describe('CreditorPurchasesTable', () => {
  let fixture: ComponentFixture<CreditorPurchasesTable>;

  const money = (value: number): Money => value as Money;

  const groups: CreditorPurchaseGroup[] = [{
    planId: 'pl-1',
    description: 'Sofa',
    purchaseDate: '2026-01-10',
    totalMinorUnits: money(300000),
    outstandingMinorUnits: money(200000),
    installments: [
      { installmentId: 'i-1', sequence: 1, installmentCount: 3, amountMinorUnits: money(100000), dueYear: 2026, dueMonth: 2, isPaid: true, isReversed: false, status: 'paid' },
      { installmentId: 'i-2', sequence: 2, installmentCount: 3, amountMinorUnits: money(100000), dueYear: 2026, dueMonth: 3, isPaid: false, isReversed: false, status: 'overdue' },
      { installmentId: 'i-3', sequence: 3, installmentCount: 3, amountMinorUnits: money(100000), dueYear: 2026, dueMonth: 4, isPaid: false, isReversed: false, status: 'future' }
    ]
  }, {
    planId: 'pl-2',
    description: 'Trip',
    purchaseDate: '2026-02-20',
    totalMinorUnits: money(60000),
    outstandingMinorUnits: money(60000),
    installments: [
      { installmentId: 'i-4', sequence: 1, installmentCount: 1, amountMinorUnits: money(60000), dueYear: 2026, dueMonth: 4, isPaid: false, isReversed: false, status: 'due' }
    ]
  }];

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [CreditorPurchasesTable],
      providers: [provideZonelessChangeDetection()]
    });
    fixture = TestBed.createComponent(CreditorPurchasesTable);
  });

  it('renders one section per purchase with its description and outstanding-of-total', () => {
    fixture.componentRef.setInput('purchases', groups);
    fixture.detectChanges();
    const sections: NodeListOf<HTMLElement> =
      fixture.nativeElement.querySelectorAll('li.purchase-group');
    expect(sections.length).toBe(2);
    expect(sections[0].textContent).toContain('Sofa');
    expect(sections[0].textContent).toContain(formatArs(money(200000)));
    expect(sections[0].textContent).toContain(formatArs(money(300000)));
    expect(sections[0].textContent).toContain('outstanding of');
  });
  it('lists each installment as "N/M", its due month and a status label', () => {
    fixture.componentRef.setInput('purchases', groups);
    fixture.detectChanges();
    const firstRows: NodeListOf<HTMLElement> =
      fixture.nativeElement.querySelectorAll('li.purchase-group:first-child tbody tr');
    expect(firstRows.length).toBe(3);
    expect(firstRows[0].textContent).toContain('1/3');
    expect(firstRows[0].textContent).toContain('Feb 2026');
    expect(firstRows[0].textContent).toContain('Paid');
    expect(firstRows[1].textContent).toContain('Overdue');
    expect(firstRows[2].textContent).toContain('Future');
  });
  it('renders the paid status as a hairline badge', () => {
    fixture.componentRef.setInput('purchases', groups);
    fixture.detectChanges();
    const badge: HTMLElement = fixture.nativeElement.querySelector('.status-badge');
    expect(badge).not.toBeNull();
    expect(badge.textContent?.trim()).toBe('Paid');
  });
  it('shows an empty note when the creditor has no purchases', () => {
    fixture.componentRef.setInput('purchases', []);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('This creditor has no recorded purchases.');
    expect(fixture.nativeElement.querySelector('table')).toBeNull();
  });

  function buttonsByLabel(label: string): HTMLButtonElement[] {
    const all: NodeListOf<HTMLButtonElement> = fixture.nativeElement.querySelectorAll('button');
    return Array.from(all).filter((button: HTMLButtonElement) => button.textContent?.trim() === label);
  }

  it('shows a Pay button per unpaid, non-reversed installment and an Undo button per paid one', () => {
    fixture.componentRef.setInput('purchases', groups);
    fixture.detectChanges();
    expect(buttonsByLabel('Pay').length).toBe(3);
    expect(buttonsByLabel('Undo').length).toBe(1);
  });
  it('shows neither action for a reversed installment', () => {
    const reversed: CreditorPurchaseGroup[] = [{
      planId: 'pl-r',
      description: 'Fridge',
      purchaseDate: '2026-01-10',
      totalMinorUnits: money(50000),
      outstandingMinorUnits: money(0),
      installments: [
        { installmentId: 'i-r', sequence: 1, installmentCount: 1, amountMinorUnits: money(50000), dueYear: 2026, dueMonth: 2, isPaid: false, isReversed: true, status: 'reversed' }
      ]
    }];
    fixture.componentRef.setInput('purchases', reversed);
    fixture.detectChanges();
    expect(buttonsByLabel('Pay').length).toBe(0);
    expect(buttonsByLabel('Undo').length).toBe(0);
  });
  it('emits payClick with the installment id when Pay is clicked', () => {
    fixture.componentRef.setInput('purchases', groups);
    fixture.detectChanges();
    let emitted: string | undefined;
    fixture.componentInstance.payClick.subscribe((id: string) => (emitted = id));
    buttonsByLabel('Pay')[0].click();
    expect(emitted).toBe('i-2');
  });
  it('emits undoClick with the installment id when Undo is clicked', () => {
    fixture.componentRef.setInput('purchases', groups);
    fixture.detectChanges();
    let emitted: string | undefined;
    fixture.componentInstance.undoClick.subscribe((id: string) => (emitted = id));
    buttonsByLabel('Undo')[0].click();
    expect(emitted).toBe('i-1');
  });
  it('disables every action button while a request is in flight', () => {
    fixture.componentRef.setInput('purchases', groups);
    fixture.componentRef.setInput('paying', true);
    fixture.detectChanges();
    const actions: HTMLButtonElement[] = [...buttonsByLabel('Pay'), ...buttonsByLabel('Undo')];
    expect(actions.length).toBe(4);
    expect(actions.every((button: HTMLButtonElement) => button.disabled)).toBe(true);
  });
});
