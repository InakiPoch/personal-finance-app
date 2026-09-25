import { provideZonelessChangeDetection } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { formatMoney } from '../../../../core/money/money';
import { Money } from '../../../../core/types/money';
import { MonthlyStatementSummary } from '../../types/monthly-statement-summary';
import { StatementsTable } from './statements-table';

describe('StatementsTable', () => {
  let fixture: ComponentFixture<StatementsTable>;

  const money = (value: number): Money => value as Money;
  const rows: MonthlyStatementSummary[] = [{
      statementId: 'st-1',
      cardId: 'card-1',
      cardName: 'Visa',
      cycleYear: 2026,
      cycleMonth: 9,
      amountDueMinorUnits: money(400000),
      isPaid: false,
      paidOnUtc: null,
      currencyCode: 'ARS'
    },{
      statementId: 'st-2',
      cardId: 'card-1',
      cardName: 'Visa',
      cycleYear: 2026,
      cycleMonth: 10,
      amountDueMinorUnits: money(250000),
      isPaid: true,
      paidOnUtc: '2026-10-20T12:00:00Z',
      currencyCode: 'ARS'
  }];

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [StatementsTable],
      providers: [provideZonelessChangeDetection()]
    });
    fixture = TestBed.createComponent(StatementsTable);
  });

  it('renders each statement with a zero-padded cycle and its amount', () => {
    fixture.componentRef.setInput('statements', rows);
    fixture.detectChanges();
    const text: string = fixture.nativeElement.textContent;
    expect(text).toContain('2026-09');
    expect(text).toContain('2026-10');
    expect(text).toContain('$');
  });
  it('flags a paid statement and no others', () => {
    fixture.componentRef.setInput('statements', rows);
    fixture.detectChanges();
    const badges: NodeListOf<HTMLElement> =
      fixture.nativeElement.querySelectorAll('.statements__badge');
    expect(badges.length).toBe(1);
    expect(badges[0].textContent).toContain('Paid');
  });
  it('emits the statement id when its Open button is clicked', () => {
    fixture.componentRef.setInput('statements', rows);
    let emitted: string | undefined;
    fixture.componentInstance.openStatement.subscribe((id: string) => (emitted = id));
    fixture.detectChanges();
    const button: HTMLButtonElement = fixture.nativeElement.querySelector('tbody tr button');
    button.click();
    expect(emitted).toBe('st-1');
  });
  it('shows an empty note when there are no statements', () => {
    fixture.componentRef.setInput('statements', []);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.statements__empty')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('table')).toBeNull();
  });
  it('renders a USD statement amount in dollar formatting, not peso formatting', () => {
    const usdRow: MonthlyStatementSummary = { ...rows[0], statementId: 'st-3', currencyCode: 'USD' };
    fixture.componentRef.setInput('statements', [usdRow]);
    fixture.detectChanges();
    const text: string = fixture.nativeElement.textContent;
    expect(text).toContain(formatMoney(usdRow.amountDueMinorUnits, 'USD'));
    expect(text).not.toContain(formatMoney(usdRow.amountDueMinorUnits, 'ARS'));
  });
});
