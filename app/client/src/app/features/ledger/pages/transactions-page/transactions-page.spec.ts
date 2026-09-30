import { provideZonelessChangeDetection } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormControl, FormGroup } from '@angular/forms';
import { Router } from '@angular/router';
import { Observable, of, throwError } from 'rxjs';
import { Money } from '../../../../core/types/money';
import { InstrumentsService } from '../../../instruments/instruments-service';
import { Instrument } from '../../../instruments/types/instrument';
import { ReportsService } from '../../../reports/reports-service';
import { TransactionFeedRow } from '../../../reports/types/transaction-feed-row';
import { TransactionsPage } from './transactions-page';

type TransactionsView = {
  form: FormGroup<{
    accountId: FormControl<string>;
    from: FormControl<string>;
    to: FormControl<string>;
  }>;
  transactions: () => TransactionFeedRow[];
  loadStatus: () => 'loading' | 'ready' | 'error';
  accounts: () => Instrument[];
  openReverse: (transactionId: string) => void;
  applyFilter: () => void;
};

describe('TransactionsPage', () => {
  let fixture: ComponentFixture<TransactionsPage>;
  let view: TransactionsView;
  let transactions: jasmine.Spy<
    (filter?: { accountId?: string; from?: string; to?: string }) => Observable<TransactionFeedRow[]>
  >;
  let navigate: jasmine.Spy<(commands: unknown[]) => Promise<boolean>>;

  const money = (value: number): Money => value as Money;

  const instruments: Instrument[] = [
    { id: 'acct-debit', type: 'debit', name: 'Checking', cutoffDate: null, nextClosingDate: null },
    { id: 'acct-cash', type: 'cash', name: 'Wallet', cutoffDate: null, nextClosingDate: null },
    { id: 'card-credit', type: 'credit', name: 'Visa', cutoffDate: 12, nextClosingDate: null }
  ];
  const feedRows: TransactionFeedRow[] = [{
    id: 'tx-1',
    postedOnUtc: '2026-09-15T10:30:00Z',
    kind: 'Income',
    description: 'Salary September',
    fromAccounts: ['Salary'],
    toAccounts: ['Checking'],
    amountMinorUnits: money(90000000),
    currencyCode: 'ARS',
    isUndoEntry: false,
    isUndone: false,
    impactLines: ['ARS 900.000 is removed from Checking.']
  }];

  beforeEach(() => {
    transactions = jasmine.createSpy('transactions').and.returnValue(of(feedRows));
    navigate = jasmine.createSpy('navigate').and.resolveTo(true);
    TestBed.configureTestingModule({
      imports: [TransactionsPage],
      providers: [
        provideZonelessChangeDetection(),
        { provide: ReportsService, useValue: { transactions } },
        { provide: InstrumentsService, useValue: { list: () => of<Instrument[]>(instruments) } },
        { provide: Router, useValue: { navigate } }
      ]
    });
    fixture = TestBed.createComponent(TransactionsPage);
    view = fixture.componentInstance as unknown as TransactionsView;
    fixture.detectChanges();
  });

  it('creates, loads the feed on init and offers only non-credit accounts in the filter', () => {
    expect(transactions).toHaveBeenCalledTimes(1);
    expect(view.loadStatus()).toBe('ready');
    expect(view.transactions()).toEqual(feedRows);
    expect(view.accounts().map((account: Instrument) => account.id)).toEqual(['acct-debit', 'acct-cash']);
  });
  it('shows the plain-words subtitle and the loaded description', () => {
    const text: string = fixture.nativeElement.textContent;
    expect(text).toContain('Pick a movement to see what undoing it would change.');
    expect(text).toContain('Salary September');
  });
  it('re-fetches with the account and date filter when Apply is pressed', () => {
    view.form.setValue({ accountId: 'acct-debit', from: '2026-09-01', to: '2026-09-30' });
    view.applyFilter();
    expect(transactions).toHaveBeenCalledWith({
      accountId: 'acct-debit',
      from: '2026-09-01',
      to: '2026-09-30'
    });
  });
  it('navigates to the id-driven reverse route when a row is reversed', () => {
    view.openReverse('tx-1');
    expect(navigate).toHaveBeenCalledWith(['ledger', 'transactions', 'tx-1', 'reverse']);
  });
  it('surfaces a load error without throwing', () => {
    transactions.and.returnValue(throwError(() => new Error('boom')));
    view.applyFilter();
    expect(view.loadStatus()).toBe('error');
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[role="alert"]')).toBeTruthy();
  });
});
