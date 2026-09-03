import { provideZonelessChangeDetection } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { Observable, of, throwError } from 'rxjs';
import { AppError } from '../../../../core/types/app-error';
import { LedgerService } from '../../ledger-service';
import { ReverseTransactionResult } from '../../types/reverse-transaction-result';
import { ReverseMovementPage } from './reverse-movement-page';

type ReverseView = {
  transactionId: () => string | null;
  status: () => 'idle' | 'reversing' | 'reversed' | 'error';
  result: () => ReverseTransactionResult | null;
  error: () => AppError | null;
  onConfirm: () => void;
};

describe('ReverseMovementPage', () => {
  let fixture: ComponentFixture<ReverseMovementPage>;
  let view: ReverseView;
  let reverse: jasmine.Spy<(transactionId: string) => Observable<ReverseTransactionResult>>;

  const reversed: ReverseTransactionResult = {
    reversalTransactionId: 'tx-2',
    originalTransactionId: 'tx-1',
    compensatingEntryPosted: true
  };

  function setup(): void {
    fixture = TestBed.createComponent(ReverseMovementPage);
    view = fixture.componentInstance as unknown as ReverseView;
  }

  beforeEach(() => {
    reverse = jasmine.createSpy('reverse').and.returnValue(of(reversed));
    TestBed.configureTestingModule({
      imports: [ReverseMovementPage],
      providers: [
        provideZonelessChangeDetection(),
        { provide: LedgerService, useValue: { reverse } },
        { provide: ActivatedRoute, useValue: { paramMap: of(convertToParamMap({ id: 'tx-1' })) } },
      ]
    });
  });

  it('picks up the transaction id from the route param', () => {
    setup();
    fixture.detectChanges();
    expect(view.transactionId()).toBe('tx-1');
    expect(view.status()).toBe('idle');
    expect(reverse).not.toHaveBeenCalled();
  });
  it('shows the append-only explanation', () => {
    setup();
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('append-only');
    expect(fixture.nativeElement.textContent).toContain('never deleted');
  });
  it('reverses on confirm and renders the append-only result', () => {
    setup();
    fixture.detectChanges();
    view.onConfirm();
    expect(reverse).toHaveBeenCalledWith('tx-1');
    expect(view.status()).toBe('reversed');
    expect(view.result()).toEqual(reversed);
    fixture.detectChanges();
    const text: string = fixture.nativeElement.textContent;
    expect(text).toContain('tx-2');
    expect(text).toContain('A card credit was posted');
  });
  it('notes when no compensating entry was posted', () => {
    reverse.and.returnValue(of({ ...reversed, compensatingEntryPosted: false }));
    setup();
    fixture.detectChanges();
    view.onConfirm();
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('plain reversing entry');
  });
  it('surfaces an AppError when the reversal is rejected', () => {
    const appError: AppError = {
      code: 'Ledger.CannotReverseAReversal',
      title: 'Conflict',
      detail: 'x',
      status: 409,
      metadata: {}
    };
    reverse.and.returnValue(throwError(() => appError));
    setup();
    fixture.detectChanges();
    view.onConfirm();
    expect(view.error()).toEqual(appError);
    expect(view.status()).toBe('error');
  });
});
