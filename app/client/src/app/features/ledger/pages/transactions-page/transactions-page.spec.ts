import { provideZonelessChangeDetection } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormControl, FormGroup } from '@angular/forms';
import { Router } from '@angular/router';
import { Observable, of, throwError } from 'rxjs';
import { Money } from '../../../../core/types/money';
import { InstrumentsService } from '../../../instruments/instruments-service';
import { Instrument } from '../../../instruments/types/instrument';
import { LedgerService } from '../../ledger-service';
import { TransactionRow } from '../../types/transaction-row';
import { TransactionsPage } from './transactions-page';

type TransactionsView = {
  form: FormGroup<{
    accountId: FormControl<string>;
    from: FormControl<string>;
    to: FormControl<string>;
  }>;
  transactions: () => TransactionRow[];
  loadStatus: () => 'loading' | 'ready' | 'error';
  accounts: () => Instrument[];
  openReverse: (transactionId: string) => void;
  applyFilter: () => void;
};

describe('TransactionsPage', () => {
  let fixture: ComponentFixture<TransactionsPage>;
  let view: TransactionsView;
  let listTransactions: jasmine.Spy<
    (filter?: { accountId?: string; from?: string; to?: string }) => Observable<TransactionRow[]>
  >;
  let navigate: jasmine.Spy<(commands: unknown[]) => Promise<boolean>>;

  const money = (value: number): Money => value as Money;

  const instruments: Instrument[] = [
    { id: 'acct-debit', type: 'debit', name: 'Checking', cutoffDate: null },
    { id: 'acct-cash', type: 'cash', name: 'Wallet', cutoffDate: null },
    { id: 'card-credit', type: 'credit', name: 'Visa', cutoffDate: 12 }
  ];
  const feedRows: TransactionRow[] = [{
    transactionId: 'tx-1',
    postedOnUtc: '2026-09-15T10:30:00Z',
    description: 'Manual entry',
    amountMinorUnits: money(500000),
    isReversal: false,
    isReversed: false,
    installmentReferenceId: null,
    splitReferenceId: null
  }];

  beforeEach(() => {
    listTransactions = jasmine.createSpy('listTransactions').and.returnValue(of(feedRows));
    navigate = jasmine.createSpy('navigate').and.resolveTo(true);
    TestBed.configureTestingModule({
      imports: [TransactionsPage],
      providers: [
        provideZonelessChangeDetection(),
        { provide: LedgerService, useValue: { listTransactions } },
        { provide: InstrumentsService, useValue: { list: () => of<Instrument[]>(instruments) } },
        { provide: Router, useValue: { navigate } }
      ]
    });
    fixture = TestBed.createComponent(TransactionsPage);
    view = fixture.componentInstance as unknown as TransactionsView;
    fixture.detectChanges();
  });

  it('creates, loads the feed on init and offers only non-credit accounts in the filter', () => {
    expect(listTransactions).toHaveBeenCalledTimes(1);
    expect(view.loadStatus()).toBe('ready');
    expect(view.transactions()).toEqual(feedRows);
    expect(view.accounts().map((account: Instrument) => account.id)).toEqual(['acct-debit', 'acct-cash']);
  });
  it('re-fetches with the account and date filter when Apply is pressed', () => {
    view.form.setValue({ accountId: 'acct-debit', from: '2026-09-01', to: '2026-09-30' });
    view.applyFilter();
    expect(listTransactions).toHaveBeenCalledWith({
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
    listTransactions.and.returnValue(throwError(() => new Error('boom')));
    view.applyFilter();
    expect(view.loadStatus()).toBe('error');
  });
});
