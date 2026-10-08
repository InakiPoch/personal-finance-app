import { provideZonelessChangeDetection } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { formatMoney } from '../../../../core/money/money';
import { Money } from '../../../../core/types/money';
import { MoneyFlowRow } from '../../types/money-flow-row';
import { MoneyFlowCurrencyTotal, MoneyFlowTable } from './money-flow-table';

describe('MoneyFlowTable', () => {
  let fixture: ComponentFixture<MoneyFlowTable>;

  const money = (value: number): Money => value as Money;

  const incomeRow: MoneyFlowRow = {
    transactionId: 'tx-1',
    date: '2026-09-24',
    description: 'Salary September',
    accountName: 'Galicia',
    kind: 'Income',
    amountMinorUnits: money(85000000),
    currencyCode: 'ARS',
    flag: null,
    partyName: null
  };
  const outcomeRow: MoneyFlowRow = {
    transactionId: 'tx-2',
    date: '2026-09-22',
    description: 'Groceries at Coto',
    accountName: 'Galicia',
    kind: 'Outcome',
    amountMinorUnits: money(4530000),
    currencyCode: 'ARS',
    flag: null,
    partyName: null
  };

  const loanRow: MoneyFlowRow = { ...outcomeRow, transactionId: 'tx-4', description: 'Lent to Lola: Rent help', flag: 'LentTo', partyName: 'Lola' };
  const sharedRow: MoneyFlowRow = { ...outcomeRow, transactionId: 'tx-5', description: 'Dinner', flag: 'SharedWith', partyName: 'Ines' };

  function render(rows: MoneyFlowRow[], footerTotals: MoneyFlowCurrencyTotal[] = []): void {
    fixture.componentRef.setInput('rows', rows);
    fixture.componentRef.setInput('footerTotals', footerTotals);
    fixture.detectChanges();
  }

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [MoneyFlowTable],
      providers: [provideZonelessChangeDetection()]
    });
    fixture = TestBed.createComponent(MoneyFlowTable);
  });

  it('renders an income row with a +, the green class, and a muted dash in the outcome cell', () => {
    render([incomeRow]);
    const cells: NodeListOf<HTMLTableCellElement> = fixture.nativeElement.querySelectorAll('tbody td');
    expect(cells[3].querySelector('.text-ledger')).toBeTruthy();
    expect(cells[3].textContent).toContain('+');
    expect(cells[4].querySelector('.text-ink-faint')?.textContent).toContain('—');
  });
  it('renders an outcome row with a U+2212 minus, the red class, and a muted dash in the income cell', () => {
    render([outcomeRow]);
    const cells: NodeListOf<HTMLTableCellElement> = fixture.nativeElement.querySelectorAll('tbody td');
    expect(cells[4].querySelector('.text-negative')).toBeTruthy();
    expect(cells[4].textContent).toContain('−');
    expect(cells[3].querySelector('.text-ink-faint')?.textContent).toContain('—');
  });
  it('formats a USD row through the USD formatter, not ARS', () => {
    const usdRow: MoneyFlowRow = { ...outcomeRow, transactionId: 'tx-3', currencyCode: 'USD', amountMinorUnits: money(5000) };
    render([usdRow]);
    const text: string = fixture.nativeElement.textContent;
    expect(text).toContain(formatMoney(money(5000), 'USD'));
  });
  it('shows the empty note when there are no rows', () => {
    render([]);
    expect(fixture.nativeElement.querySelector('.flow-empty')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('table')).toBeNull();
  });
  it('renders one footer line per currency with the income and outcome totals', () => {
    render([incomeRow, outcomeRow], [
      { currencyCode: 'ARS', income: money(85000000), outcome: money(4530000) },
      { currencyCode: 'USD', income: money(0), outcome: money(5000) }
    ]);
    const footerRows: NodeListOf<HTMLTableRowElement> = fixture.nativeElement.querySelectorAll('tfoot tr');
    expect(footerRows.length).toBe(2);
    expect(footerRows[0].textContent).toContain('ARS');
    expect(footerRows[0].textContent).toContain(formatMoney(money(85000000), 'ARS'));
    expect(footerRows[1].textContent).toContain('USD');
    expect(footerRows[1].textContent).toContain(formatMoney(money(5000), 'USD'));
  });
  it('renders an Undo button on an income row but not on an outcome row', () => {
    render([incomeRow, outcomeRow]);
    const bodyRows: NodeListOf<HTMLTableRowElement> = fixture.nativeElement.querySelectorAll('tbody tr');
    expect(bodyRows[0].querySelector('button')?.textContent?.trim()).toBe('Undo');
    expect(bodyRows[1].querySelector('button')).toBeNull();
  });
  it('emits the transaction id when the Undo button is clicked', () => {
    render([incomeRow]);
    const emitted: string[] = [];
    fixture.componentInstance.undo.subscribe((transactionId: string) => emitted.push(transactionId));
    (fixture.nativeElement.querySelector('tbody button') as HTMLButtonElement).click();
    expect(emitted).toEqual(['tx-1']);
  });
  it('disables the Undo button while undoing() is true', () => {
    render([incomeRow]);
    fixture.componentRef.setInput('undoing', true);
    fixture.detectChanges();
    expect((fixture.nativeElement.querySelector('tbody button') as HTMLButtonElement).disabled).toBeTrue();
  });
  it('flags a loan row "Lent to <party>" and offers Undo on it', () => {
    render([loanRow]);
    expect(fixture.nativeElement.querySelector('tbody tr').textContent).toContain('Lent to Lola');
    expect(fixture.nativeElement.querySelector('tbody button')?.textContent?.trim()).toBe('Undo');
  });
  it('flags a settlement income row "Paid back by <party>" and offers Undo on it', () => {
    render([{ ...incomeRow, transactionId: 'tx-6', flag: 'PaidBackBy', partyName: 'Lola' }]);
    expect(fixture.nativeElement.querySelector('tbody tr').textContent).toContain('Paid back by Lola');
    expect(fixture.nativeElement.querySelector('tbody button')?.textContent?.trim()).toBe('Undo');
  });
  it('flags a shared expense "Shared with <party>" without an Undo button', () => {
    render([sharedRow]);
    expect(fixture.nativeElement.querySelector('tbody tr').textContent).toContain('Shared with Ines');
    expect(fixture.nativeElement.querySelector('tbody button')).toBeNull();
  });
  it('emits the transaction id when Undo is clicked on a loan row', () => {
    render([loanRow]);
    const emitted: string[] = [];
    fixture.componentInstance.undo.subscribe((transactionId: string) => emitted.push(transactionId));
    (fixture.nativeElement.querySelector('tbody button') as HTMLButtonElement).click();
    expect(emitted).toEqual(['tx-4']);
  });
});
