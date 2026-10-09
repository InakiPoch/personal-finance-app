import { provideZonelessChangeDetection } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormControl, FormGroup } from '@angular/forms';
import { Observable, of, throwError } from 'rxjs';
import { AppError } from '../../../../core/types/app-error';
import { CurrencyCode } from '../../../../core/types/currency-code';
import { Money } from '../../../../core/types/money';
import { LedgerService } from '../../../ledger/ledger-service';
import { PartiesService } from '../../parties-service';
import { PartyPurchaseKind } from '../../types/party-purchase-kind';
import { PartyPurchaseResult } from '../../types/party-purchase-result';
import { RecordPartyPurchase } from '../../types/record-party-purchase';
import { PartyPurchaseForm } from './party-purchase-form';

type PartyPurchaseFormView = {
  form: FormGroup<{
    kind: FormControl<PartyPurchaseKind>;
    share: FormControl<number | null>;
    currency: FormControl<CurrencyCode>;
    categoryName: FormControl<string>;
    description: FormControl<string>;
    purchaseDate: FormControl<string>;
  }>;
  status: () => 'idle' | 'saving' | 'saved' | 'error';
  error: () => AppError | null;
  onSubmit: () => void;
};

const money = (value: number): Money => value as Money;

describe('PartyPurchaseForm', () => {
  let fixture: ComponentFixture<PartyPurchaseForm>;
  let view: PartyPurchaseFormView;
  let recordPartyPurchase: jasmine.Spy<(id: string, body: RecordPartyPurchase) => Observable<PartyPurchaseResult>>;
  let recorded: jasmine.Spy<() => void>;

  function text(): string {
    return (fixture.nativeElement as HTMLElement).textContent ?? '';
  }

  function fill(): void {
    view.form.setValue({
      kind: 'debit',
      share: 120,
      currency: 'ARS',
      categoryName: ' Eating out ',
      description: ' Dinner ',
      purchaseDate: '2026-09-10'
    });
  }

  beforeEach(() => {
    recordPartyPurchase = jasmine
      .createSpy('recordPartyPurchase')
      .and.returnValue(of<PartyPurchaseResult>({ purchaseId: 'pu-1' }));
    recorded = jasmine.createSpy('recorded');
    TestBed.configureTestingModule({
      imports: [PartyPurchaseForm],
      providers: [
        provideZonelessChangeDetection(),
        { provide: PartiesService, useValue: { recordPartyPurchase } },
        { provide: LedgerService, useValue: { listExpenseCategories: () => of(['Groceries']) } }
      ]
    });
    fixture = TestBed.createComponent(PartyPurchaseForm);
    fixture.componentRef.setInput('partyId', 'p1');
    fixture.componentRef.setInput('partyName', 'Alice');
    fixture.componentInstance.recorded.subscribe(recorded);
    view = fixture.componentInstance as unknown as PartyPurchaseFormView;
    fixture.detectChanges();
  });

  it('is titled after the party and offers existing categories', () => {
    expect(text()).toContain('Paid by Alice');
    expect(fixture.nativeElement.querySelector('option[value="Groceries"]')).not.toBeNull();
  });
  it('has a debit/credit toggle with debit selected and credit not yet available', () => {
    const buttons: HTMLButtonElement[] = Array.from(fixture.nativeElement.querySelectorAll('fieldset button'));
    expect(buttons.map((button: HTMLButtonElement) => button.textContent?.trim())).toEqual(['Debit', 'Credit']);
    expect(buttons[0].getAttribute('aria-pressed')).toBe('true');
    expect(buttons[1].disabled).toBe(true);
  });
  it('stays invalid until share, category, description and a non-future date are set', () => {
    expect(view.form.valid).toBe(false);
    fill();
    expect(view.form.valid).toBe(true);
    view.form.patchValue({ share: 0 });
    expect(view.form.valid).toBe(false);
    view.form.patchValue({ share: 120, categoryName: '  ' });
    expect(view.form.valid).toBe(false);
    view.form.patchValue({ categoryName: 'Food', description: 'two\nlines' });
    expect(view.form.valid).toBe(false);
    view.form.patchValue({ description: 'ok', purchaseDate: '2999-01-01' });
    expect(view.form.valid).toBe(false);
  });
  it('submits a trimmed minor-units debit purchase and emits recorded', () => {
    fill();
    view.onSubmit();
    const [id, body]: [string, RecordPartyPurchase] = recordPartyPurchase.calls.mostRecent().args;
    expect(id).toBe('p1');
    expect(body).toEqual({
      shareMinorUnits: money(12000),
      currencyCode: 'ARS',
      description: 'Dinner',
      categoryName: 'Eating out',
      purchaseDate: '2026-09-10',
      kind: 'debit',
      today: new Date().toLocaleDateString('sv-SE')
    });
    expect(view.status()).toBe('saved');
    expect(recorded).toHaveBeenCalledTimes(1);
  });
  it('does not submit an invalid form', () => {
    view.onSubmit();
    expect(recordPartyPurchase).not.toHaveBeenCalled();
  });
  it('shows a message keyed off the AppError code when the purchase is rejected', () => {
    const appError: AppError = { code: 'Parties.PurchaseDateInFuture', title: 'x', detail: 'x', status: 422, metadata: {} };
    recordPartyPurchase.and.returnValue(throwError(() => appError));
    fill();
    view.onSubmit();
    fixture.detectChanges();
    expect(view.status()).toBe('error');
    expect(text()).toContain('The purchase date cannot be in the future.');
    expect(recorded).not.toHaveBeenCalled();
  });
});
