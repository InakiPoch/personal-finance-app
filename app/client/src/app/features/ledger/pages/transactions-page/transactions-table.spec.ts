import { provideZonelessChangeDetection } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Money } from '../../../../core/types/money';
import { TransactionFeedRow } from '../../../reports/types/transaction-feed-row';
import { TransactionsTable } from './transactions-table';

describe('TransactionsTable', () => {
  let fixture: ComponentFixture<TransactionsTable>;

  const money = (value: number): Money => value as Money;
  const active: TransactionFeedRow = {
    id: 'tx-1',
    postedOnUtc: '2026-09-15T10:30:00Z',
    kind: 'Card installment',
    description: 'Notebook — installment 3 of 12 on Visa',
    fromAccounts: ['Visa purchases'],
    toAccounts: ['What you owe on Visa'],
    amountMinorUnits: money(4000000),
    currencyCode: 'ARS',
    isUndoEntry: false,
    isUndone: false,
    impactLines: ['Your Visa bill goes down by ARS 40.000.', 'Juan no longer owes you ARS 20.000.']
  };
  const other: TransactionFeedRow = {
    ...active,
    id: 'tx-2',
    kind: 'Income',
    description: 'Salary September',
    fromAccounts: ['Salary'],
    toAccounts: ['Galicia checking'],
    impactLines: ['ARS 900.000 is removed from Galicia checking.']
  };
  const undoEntry: TransactionFeedRow = {
    ...active,
    id: 'tx-3',
    kind: 'Undo entry',
    description: 'Undid: Groceries at Coto',
    isUndoEntry: true,
    impactLines: []
  };
  const undone: TransactionFeedRow = {
    ...active,
    id: 'tx-4',
    kind: 'Expense',
    description: 'Groceries at Coto',
    isUndone: true
  };
  const rowsEl = (): HTMLElement[] => Array.from(fixture.nativeElement.querySelectorAll('tr.txn-row'));
  const chevron = (row: HTMLElement): HTMLButtonElement | null => row.querySelector('button[aria-expanded]');
  const render = (rows: TransactionFeedRow[]): void => {
    fixture.componentRef.setInput('transactions', rows);
    fixture.detectChanges();
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [TransactionsTable],
      providers: [provideZonelessChangeDetection()]
    });
    fixture = TestBed.createComponent(TransactionsTable);
  });

  it('renders date, badge, description, from → to and the amount', () => {
    render([active]);
    const text: string = fixture.nativeElement.textContent;
    expect(text).toContain('2026-09-15');
    expect(fixture.nativeElement.querySelector('.status-badge').textContent).toContain('Card installment');
    expect(text).toContain('Notebook — installment 3 of 12 on Visa');
    expect(text).toContain('Visa purchases → What you owe on Visa');
    expect(text).toContain('$');
  });
  it('formats a USD row with the USD formatter', () => {
    render([{ ...active, currencyCode: 'USD', amountMinorUnits: money(5000) }]);
    const text: string = fixture.nativeElement.textContent;
    expect(text).toMatch(/US\$|\$/);
    expect(text).toContain('50.00');
  });
  it('keeps impact lines and the Reverse button hidden until the chevron is clicked', () => {
    render([active]);
    expect(fixture.nativeElement.textContent).not.toContain('If you reverse this');
    expect(fixture.nativeElement.textContent).not.toContain('Reverse…');
    chevron(rowsEl()[0])!.click();
    fixture.detectChanges();
    const text: string = fixture.nativeElement.textContent;
    expect(text).toContain('If you reverse this');
    expect(text).toContain('Your Visa bill goes down by ARS 40.000.');
    expect(text).toContain('Juan no longer owes you ARS 20.000.');
  });
  it('expands a single row at a time', () => {
    render([active, other]);
    chevron(rowsEl()[0])!.click();
    fixture.detectChanges();
    chevron(rowsEl()[1])!.click();
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelectorAll('section h3').length).toBe(1);
    const text: string = fixture.nativeElement.textContent;
    expect(text).toContain('ARS 900.000 is removed from Galicia checking.');
    expect(text).not.toContain('Your Visa bill goes down');
  });
  it('emits the transaction id from the Reverse… button inside the panel', () => {
    render([active]);
    let emitted: string | undefined;
    fixture.componentInstance.reverseTransaction.subscribe((id: string) => (emitted = id));
    chevron(rowsEl()[0])!.click();
    fixture.detectChanges();
    const button: HTMLButtonElement = fixture.nativeElement.querySelector('section button');
    expect(button.textContent).toContain('Reverse…');
    button.click();
    expect(emitted).toBe('tx-1');
  });
  it('locks an undo entry: badge, "Undo entry" label, no chevron, no button', () => {
    render([undoEntry]);
    const row: HTMLElement = rowsEl()[0];
    expect(row.querySelector('.status-badge')!.textContent).toContain('Undo entry');
    expect(row.querySelector('.transactions__note')!.textContent).toContain('Undo entry');
    expect(chevron(row)).toBeNull();
    expect(fixture.nativeElement.querySelector('button')).toBeNull();
  });
  it('locks an undone row: "Already undone" and no Reverse button even when expanded', () => {
    render([undone]);
    const row: HTMLElement = rowsEl()[0];
    expect(row.querySelector('.transactions__note')!.textContent).toContain('Already undone');
    chevron(row)?.click();
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).not.toContain('Reverse…');
  });
  it('shows an empty note when there are no transactions', () => {
    render([]);
    expect(fixture.nativeElement.querySelector('.transactions__empty')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('table')).toBeNull();
  });
});
