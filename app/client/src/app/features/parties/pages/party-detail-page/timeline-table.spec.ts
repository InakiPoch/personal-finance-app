import { provideZonelessChangeDetection } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Money } from '../../../../core/types/money';
import { PartyTimelineRow } from '../../../reports/types/party-timeline-row';
import { TimelineTable } from './timeline-table';

const money = (value: number): Money => value as Money;

const rows: PartyTimelineRow[] = [{
    movementOnUtc: '2026-09-01T20:00:00.000Z',
    description: 'Dinner split',
    deltaMinorUnits: money(300000),
    runningBalanceMinorUnits: money(300000),
    currencyCode: 'ARS'
  }, {
    movementOnUtc: '2026-09-05T10:00:00.000Z',
    description: 'Reversal of dinner split',
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
    expect(text()).toContain('Dinner split');
    expect(text()).toContain('+$');
    expect(text()).toContain('-$');
  });
  it('shows the reversal row as a negative delta with its description', () => {
    fixture.componentRef.setInput('rows', rows);
    fixture.detectChanges();
    const debitCell: HTMLElement | null = fixture.nativeElement.querySelector('.timeline__debit');
    expect(debitCell?.textContent).toContain('-$');
    expect(text()).toContain('Reversal of dinner split');
  });
  it('shows an empty note when there are no rows', () => {
    fixture.componentRef.setInput('rows', []);
    fixture.detectChanges();
    expect(text()).toContain('No movements recorded');
  });
});
