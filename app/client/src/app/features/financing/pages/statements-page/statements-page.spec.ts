import { provideZonelessChangeDetection } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormControl, FormGroup } from '@angular/forms';
import { Router } from '@angular/router';
import { Observable, of, throwError } from 'rxjs';
import { Money } from '../../../../core/types/money';
import { InstrumentsService } from '../../../instruments/instruments-service';
import { Instrument } from '../../../instruments/types/instrument';
import { FinancingService } from '../../financing-service';
import { MonthlyStatementSummary } from '../../types/monthly-statement-summary';
import { StatementsPage } from './statements-page';

type StatementsView = {
  form: FormGroup<{ cardId: FormControl<string> }>;
  statements: () => MonthlyStatementSummary[];
  loadStatus: () => 'idle' | 'loading' | 'ready' | 'error';
  creditCards: () => Instrument[];
  openStatement: (statementId: string) => void;
};

describe('StatementsPage', () => {
  let fixture: ComponentFixture<StatementsPage>;
  let view: StatementsView;
  let listStatements: jasmine.Spy<(cardId: string) => Observable<MonthlyStatementSummary[]>>;
  let navigate: jasmine.Spy<(commands: unknown[]) => Promise<boolean>>;

  const money = (value: number): Money => value as Money;

  const instruments: Instrument[] = [
    { id: 'card-credit', type: 'credit', name: 'Visa', cutoffDate: 12 },
    { id: 'acct-debit', type: 'debit', name: 'Checking', cutoffDate: null }
  ];
  const statementRows: MonthlyStatementSummary[] = [{
    statementId: 'st-1',
    cardId: 'card-credit',
    cardName: 'Visa',
    cycleYear: 2026,
    cycleMonth: 9,
    amountDueMinorUnits: money(400000),
    isPaid: false,
    paidOnUtc: null,
    currencyCode: 'ARS'
  }];

  beforeEach(() => {
    listStatements = jasmine.createSpy('listStatements').and.returnValue(of(statementRows));
    navigate = jasmine.createSpy('navigate').and.resolveTo(true);
    TestBed.configureTestingModule({
      imports: [StatementsPage],
      providers: [
        provideZonelessChangeDetection(),
        { provide: FinancingService, useValue: { listStatements } },
        { provide: InstrumentsService, useValue: { list: () => of<Instrument[]>(instruments) } },
        { provide: Router, useValue: { navigate } }
      ],
    });
    fixture = TestBed.createComponent(StatementsPage);
    view = fixture.componentInstance as unknown as StatementsView;
    fixture.detectChanges();
  });

  it('creates and offers only credit cards in the picker', () => {
    expect(fixture.componentInstance).toBeTruthy();
    expect(view.creditCards().map((card: Instrument) => card.id)).toEqual(['card-credit']);
    expect(view.loadStatus()).toBe('idle');
  });
  it('fetches and renders the statements for the picked card', () => {
    view.form.controls.cardId.setValue('card-credit');
    expect(listStatements).toHaveBeenCalledOnceWith('card-credit');
    expect(view.loadStatus()).toBe('ready');
    expect(view.statements()).toEqual(statementRows);
  });
  it('does not fetch when the picker is cleared back to the empty option', () => {
    view.form.controls.cardId.setValue('');
    expect(listStatements).not.toHaveBeenCalled();
    expect(view.loadStatus()).toBe('idle');
  });
  it('navigates to the statement detail route when a row is opened', () => {
    view.openStatement('st-1');
    expect(navigate).toHaveBeenCalledWith(['financing', 'statements', 'st-1']);
  });
  it('surfaces a load error without throwing', () => {
    listStatements.and.returnValue(throwError(() => new Error('boom')));
    view.form.controls.cardId.setValue('card-credit');
    expect(view.loadStatus()).toBe('error');
    expect(view.statements()).toEqual([]);
  });
});
