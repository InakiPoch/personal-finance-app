import { provideZonelessChangeDetection } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Money } from '../../../../core/types/money';
import { PartyTimelineRow } from '../../../reports/types/party-timeline-row';
import { TimelineTable } from './timeline-table';

const money = (value: number): Money => value as Money;

const rows: PartyTimelineRow[] = [{
    transactionId: 'tx-1',
    movementOnUtc: '2026-09-01T20:00:00.000Z',
    description: 'Shared expense',
    deltaMinorUnits: money(300000),
    runningBalanceMinorUnits: money(300000),
    currencyCode: 'ARS'
  }, {
    transactionId: 'tx-2',
    movementOnUtc: '2026-09-05T10:00:00.000Z',
    description: 'Reversal',
    deltaMinorUnits: money(-300000),
    runningBalanceMinorUnits: money(0),
    currencyCode: 'ARS'
  }
];

describe('TimelineTable', () => {
  let fixture: ComponentFixture<TimelineTable>;

  function text(): string {
    return (fixture.nativeElement as HTMLElement).textContent ?? '';
  }

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [TimelineTable],
      providers: [provideZonelessChangeDetection()]
    });
    fixture = TestBed.createComponent(TimelineTable);
  });

  it('renders each movement with a signed delta and running balance', () => {
    fixture.componentRef.setInput('rows', rows);
    fixture.detectChanges();
    expect(text()).toContain('Shared expense');
    expect(text()).toContain('+$');
    expect(text()).toContain('-$');
  });
  it('shows the reversal row as a negative delta with its description', () => {
    fixture.componentRef.setInput('rows', rows);
    fixture.detectChanges();
    const debitCell: HTMLElement | null = fixture.nativeElement.querySelector('.timeline__debit');
    expect(debitCell?.textContent).toContain('-$');
    expect(text()).toContain('Reversal');
  });
  it('emits the transaction id when an active Reverse button is clicked', () => {
    fixture.componentRef.setInput('rows', rows);
    let emitted: string | undefined;
    fixture.componentInstance.reverseClick.subscribe((id: string) => (emitted = id));
    fixture.detectChanges();
    const button: HTMLButtonElement = fixture.nativeElement.querySelector('tbody tr button');
    button.click();
    expect(emitted).toBe('tx-1');
  });
  it('locks the Reverse action on a reversal row', () => {
    fixture.componentRef.setInput('rows', rows);
    fixture.detectChanges();
    const buttons: NodeListOf<HTMLButtonElement> =
      fixture.nativeElement.querySelectorAll('tbody tr button');
    expect(buttons[0].disabled).toBeFalse();
    expect(buttons[1].disabled).toBeTrue();
    expect(fixture.nativeElement.querySelector('.timeline__note').textContent).toContain('Undo entry');
  });
  describe('party purchase rows', () => {
    const purchaseRow: PartyTimelineRow = {
      transactionId: 'tx-p1',
      movementOnUtc: '2026-09-01T00:00:00.000Z',
      description: 'Paid by Alice: Dinner',
      deltaMinorUnits: money(12000),
      runningBalanceMinorUnits: money(12000),
      currencyCode: 'ARS',
      purchaseId: 'pu-1'
    };
    const undoRow: PartyTimelineRow = {
      transactionId: 'tx-p2',
      movementOnUtc: '2026-09-02T00:00:00.000Z',
      description: 'Reversal',
      deltaMinorUnits: money(-12000),
      runningBalanceMinorUnits: money(0),
      currencyCode: 'ARS',
      purchaseId: 'pu-1'
    };

    it('offers Undo purchase and emits the purchase id, not the transaction id', () => {
      fixture.componentRef.setInput('rows', [purchaseRow]);
      let emitted: string | undefined;
      let reversed: string | undefined;
      fixture.componentInstance.undoPurchaseClick.subscribe((id: string) => (emitted = id));
      fixture.componentInstance.reverseClick.subscribe((id: string) => (reversed = id));
      fixture.detectChanges();
      const button: HTMLButtonElement = fixture.nativeElement.querySelector('tbody tr button');
      expect(button.textContent).toContain('Undo purchase');
      button.click();
      expect(emitted).toBe('pu-1');
      expect(reversed).toBeUndefined();
    });
    it('locks Undo purchase once the purchase has been undone', () => {
      fixture.componentRef.setInput('rows', [purchaseRow, undoRow]);
      fixture.detectChanges();
      const buttons: NodeListOf<HTMLButtonElement> = fixture.nativeElement.querySelectorAll('tbody tr button');
      expect(buttons[0].disabled).toBeTrue();
      expect(buttons[1].disabled).toBeTrue();
    });
  });
  it('shows an empty note when there are no rows', () => {
    fixture.componentRef.setInput('rows', []);
    fixture.detectChanges();
    expect(text()).toContain('No movements recorded');
  });
});
