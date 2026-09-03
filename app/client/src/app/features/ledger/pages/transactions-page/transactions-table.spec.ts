import { provideZonelessChangeDetection } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Money } from '../../../../core/types/money';
import { TransactionRow } from '../../types/transaction-row';
import { TransactionsTable } from './transactions-table';

describe('TransactionsTable', () => {
  let fixture: ComponentFixture<TransactionsTable>;

  const money = (value: number): Money => value as Money;
  const rows: TransactionRow[] = [{
      transactionId: 'tx-1',
      postedOnUtc: '2026-09-15T10:30:00Z',
      description: 'Manual entry',
      amountMinorUnits: money(500000),
      isReversal: false,
      isReversed: false,
      installmentReferenceId: null,
      splitReferenceId: null
    },{
      transactionId: 'tx-2',
      postedOnUtc: '2026-09-16T09:00:00Z',
      description: 'Reversal',
      amountMinorUnits: money(500000),
      isReversal: true,
      isReversed: false,
      installmentReferenceId: null,
      splitReferenceId: null
  }];

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [TransactionsTable],
      providers: [provideZonelessChangeDetection()]
    });
    fixture = TestBed.createComponent(TransactionsTable);
  });

  it('renders each transaction with its posted date, label and amount', () => {
    fixture.componentRef.setInput('transactions', rows);
    fixture.detectChanges();
    const text: string = fixture.nativeElement.textContent;
    expect(text).toContain('2026-09-15');
    expect(text).toContain('Manual entry');
    expect(text).toContain('$');
  });
  it('locks the Reverse action on a reversal or already-reversed row', () => {
    fixture.componentRef.setInput('transactions', rows);
    fixture.detectChanges();
    const buttons: NodeListOf<HTMLButtonElement> =
      fixture.nativeElement.querySelectorAll('tbody tr button');
    expect(buttons[0].disabled).toBeFalse();
    expect(buttons[1].disabled).toBeTrue();
    expect(fixture.nativeElement.querySelector('.transactions__note').textContent)
      .toContain('Reversal entry');
  });
  it('emits the transaction id when an active Reverse button is clicked', () => {
    fixture.componentRef.setInput('transactions', rows);
    let emitted: string | undefined;
    fixture.componentInstance.reverseTransaction.subscribe((id: string) => (emitted = id));
    fixture.detectChanges();
    const button: HTMLButtonElement = fixture.nativeElement.querySelector('tbody tr button');
    button.click();
    expect(emitted).toBe('tx-1');
  });
  it('shows an empty note when there are no transactions', () => {
    fixture.componentRef.setInput('transactions', []);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.transactions__empty')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('table')).toBeNull();
  });
});
