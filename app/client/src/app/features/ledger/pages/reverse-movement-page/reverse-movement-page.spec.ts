import { provideZonelessChangeDetection } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { NEVER, Observable, of, throwError } from 'rxjs';
import { AppError } from '../../../../core/types/app-error';
import { Money } from '../../../../core/types/money';
import { ReportsService } from '../../../reports/reports-service';
import { TransactionFeedRow } from '../../../reports/types/transaction-feed-row';
import { LedgerService } from '../../ledger-service';
import { ReverseTransactionResult } from '../../types/reverse-transaction-result';
import { ReverseMovementPage } from './reverse-movement-page';

describe('ReverseMovementPage', () => {
  let fixture: ComponentFixture<ReverseMovementPage>;
  let reverse: jasmine.Spy<(transactionId: string) => Observable<ReverseTransactionResult>>;
  let transaction: jasmine.Spy<(id: string) => Observable<TransactionFeedRow>>;

  const reversed: ReverseTransactionResult = {
    reversalTransactionId: 'tx-2',
    originalTransactionId: 'a1b2c3d4-0000-0000-0000-000000000001',
    compensatingEntryPosted: true
  };
  const id: string = 'a1b2c3d4-0000-0000-0000-000000000001';
  const row: TransactionFeedRow = {
    id,
    postedOnUtc: '2026-09-15T10:30:00Z',
    kind: 'Card installment',
    description: 'Notebook — installment 3 of 12 on Visa',
    fromAccounts: ['Visa purchases'],
    toAccounts: ['What you owe on Visa'],
    amountMinorUnits: 4000000 as Money,
    currencyCode: 'ARS',
    isUndoEntry: false,
    isUndone: false,
    impactLines: ['Your Visa bill goes down by ARS 40.000.', 'Juan no longer owes you ARS 20.000.']
  };
  const appError = (code: string, status: number = 409): AppError =>
    ({ code, title: 'x', detail: 'x', status, metadata: {} });

  const text = (): string => fixture.nativeElement.textContent;
  const confirmButton = (): HTMLButtonElement | null =>
    Array.from<HTMLButtonElement>(fixture.nativeElement.querySelectorAll('button'))
      .find((b: HTMLButtonElement) => b.textContent!.includes('Undo this transaction')) ?? null;
  const setup = (): void => {
    fixture = TestBed.createComponent(ReverseMovementPage);
    fixture.detectChanges();
  };
  const confirm = (): void => {
    confirmButton()!.click();
    fixture.detectChanges();
  };

  beforeEach(() => {
    reverse = jasmine.createSpy('reverse').and.returnValue(of(reversed));
    transaction = jasmine.createSpy('transaction').and.returnValue(of(row));
    TestBed.configureTestingModule({
      imports: [ReverseMovementPage],
      providers: [
        provideZonelessChangeDetection(),
        provideRouter([]),
        { provide: LedgerService, useValue: { reverse } },
        { provide: ReportsService, useValue: { transaction } },
        { provide: ActivatedRoute, useValue: { paramMap: of(convertToParamMap({ id })) } },
      ]
    });
  });

  it('fetches the transaction by the route id and renders description, badge, amount, route and impact', () => {
    setup();
    expect(transaction).toHaveBeenCalledWith(id);
    expect(fixture.nativeElement.querySelector('.status-badge').textContent).toContain('Card installment');
    expect(text()).toContain('Notebook — installment 3 of 12 on Visa');
    expect(text()).toContain('$');
    expect(text()).toContain('Visa purchases → What you owe on Visa');
    expect(text()).toContain('If you reverse this');
    expect(text()).toContain('Your Visa bill goes down by ARS 40.000.');
    expect(text()).toContain('Juan no longer owes you ARS 20.000.');
  });
  it('shows no GUID, <code> element or jargon', () => {
    setup();
    expect(text()).not.toContain(id);
    expect(fixture.nativeElement.querySelector('code')).toBeNull();
    expect(text()).not.toMatch(/append-only|compensating|API|storno/i);
    expect(text()).toContain('The original stays in your history; an undo entry is added next to it.');
  });
  it('shows a loading state while the transaction loads', () => {
    transaction.and.returnValue(NEVER);
    setup();
    expect(text()).toContain('Loading transaction…');
    expect(confirmButton()).toBeNull();
  });
  it('posts the reversal once on confirm and shows the success message with the card credit note', () => {
    setup();
    confirm();
    expect(reverse).toHaveBeenCalledOnceWith(id);
    expect(text()).toContain('Transaction undone');
    expect(text()).toContain('A credit was added to your next card bill.');
    expect(confirmButton()).toBeNull();
  });
  it('omits the card credit note when no compensating entry was posted', () => {
    reverse.and.returnValue(of({ ...reversed, compensatingEntryPosted: false }));
    setup();
    confirm();
    expect(text()).toContain('An undo entry was added next to the original.');
    expect(text()).not.toContain('A credit was added');
  });
  const errorCases: [string, string][] = [
    ['Ledger.TransactionAlreadyReversed', 'This transaction was already undone.'],
    ['Ledger.CannotReverseAReversal', "An undo entry can't be undone again."],
    ['Http.Conflict', "This transaction can't be undone in its current state."],
    ['Something.Else', "The transaction couldn't be undone."],
  ];
  errorCases.forEach(([code, sentence]: [string, string]) => {
    it(`shows "${sentence}" for ${code}`, () => {
      reverse.and.returnValue(throwError(() => appError(code)));
      setup();
      confirm();
      const alert: HTMLElement = fixture.nativeElement.querySelector('[role="alert"]');
      expect(alert.textContent).toContain(sentence);
    });
  });
  it('shows the not-found state with a back link and no confirm button on any 404', () => {
    transaction.and.returnValue(throwError(() => appError('Http.NotFound', 404)));
    setup();
    expect(text()).toContain("We couldn't find that transaction.");
    expect(fixture.nativeElement.querySelector('a[href="/ledger/transactions"]')).toBeTruthy();
    expect(confirmButton()).toBeNull();
  });
  it('shows a load error (not not-found) for a non-404 failure', () => {
    transaction.and.returnValue(throwError(() => appError('Http.ServerError', 500)));
    setup();
    expect(text()).toContain("We couldn't load this transaction.");
    expect(confirmButton()).toBeNull();
  });
  it('hides the confirm button for an undo entry', () => {
    transaction.and.returnValue(of({ ...row, isUndoEntry: true, kind: 'Undo entry', impactLines: [] }));
    setup();
    expect(text()).toContain("An undo entry can't be undone again.");
    expect(confirmButton()).toBeNull();
  });
  it('hides the confirm button for an already-undone transaction', () => {
    transaction.and.returnValue(of({ ...row, isUndone: true }));
    setup();
    expect(text()).toContain('This transaction was already undone.');
    expect(confirmButton()).toBeNull();
  });
});
