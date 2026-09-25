import { provideZonelessChangeDetection } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormControl, FormGroup } from '@angular/forms';
import { Observable, of, throwError } from 'rxjs';
import { AppError } from '../../../../core/types/app-error';
import { CurrencyCode } from '../../../../core/types/currency-code';
import { Money } from '../../../../core/types/money';
import { InstrumentsService } from '../../../instruments/instruments-service';
import { Instrument } from '../../../instruments/types/instrument';
import { SubscriptionsService } from '../../subscriptions-service';
import { ActiveSubscription } from '../../types/active-subscription';
import { CreateSubscription } from '../../types/create-subscription';
import { Frequency } from '../../types/frequency';
import { PaySubscriptionResult } from '../../types/pay-subscription-result';
import { SubscriptionResult } from '../../types/subscription-result';
import { SubscriptionsPage } from './subscriptions-page';

type SubscriptionsView = {
  form: FormGroup<{
    name: FormControl<string>;
    amount: FormControl<number | null>;
    currency: FormControl<CurrencyCode>;
    category: FormControl<string>;
    fundingAccountId: FormControl<string>;
    frequency: FormControl<Frequency>;
    anchorDay: FormControl<number | null>;
  }>;
  listStatus: () => 'loading' | 'ready' | 'error';
  submitStatus: () => 'idle' | 'submitting' | 'error';
  submitError: () => AppError | null;
  active: () => ActiveSubscription[];
  onSubmit: () => void;
  onCancel: (id: string) => void;
  cancelStatusFor: (id: string) => 'idle' | 'cancelling' | 'error';
  cancelErrorFor: (id: string) => AppError | null;
  onPay: (id: string) => void;
  payStatusFor: (id: string) => 'idle' | 'paying' | 'error';
  payErrorFor: (id: string) => AppError | null;
  onUndo: (id: string) => void;
  undoStatusFor: (id: string) => 'idle' | 'undoing' | 'error';
  undoErrorFor: (id: string) => AppError | null;
};

const activeRow: ActiveSubscription = {
  subscriptionId: 'sub-1',
  name: 'Netflix',
  amountMinorUnits: 500000 as Money,
  category: 'Entertainment',
  frequency: 'monthly',
  anchorDay: 15,
  nextDueDate: '2026-10-15',
  status: 'upcoming',
  currencyCode: 'ARS'
};

const instruments: Instrument[] = [
  { id: 'acc-1', type: 'debit', name: 'Checking', cutoffDate: null },
  { id: 'card-1', type: 'credit', name: 'Visa', cutoffDate: 20 }
];

describe('SubscriptionsPage', () => {
  let fixture: ComponentFixture<SubscriptionsPage>;
  let view: SubscriptionsView;
  let listActive: jasmine.Spy<() => Observable<ActiveSubscription[]>>;
  let create: jasmine.Spy<(body: CreateSubscription) => Observable<SubscriptionResult>>;
  let cancel: jasmine.Spy<(id: string) => Observable<void>>;
  let pay: jasmine.Spy<(id: string) => Observable<PaySubscriptionResult>>;
  let unpay: jasmine.Spy<(id: string) => Observable<PaySubscriptionResult>>;

  function setup(): void {
    fixture = TestBed.createComponent(SubscriptionsPage);
    view = fixture.componentInstance as unknown as SubscriptionsView;
    fixture.detectChanges();
  }

  function text(): string {
    return (fixture.nativeElement as HTMLElement).textContent ?? '';
  }

  function buttonsByLabel(label: string): HTMLButtonElement[] {
    const buttons: NodeListOf<HTMLButtonElement> = (fixture.nativeElement as HTMLElement)
      .querySelectorAll<HTMLButtonElement>('button');
    return Array.from(buttons).filter((button: HTMLButtonElement) => button.textContent?.trim() === label);
  }

  beforeEach(() => {
    listActive = jasmine
      .createSpy('listActive')
      .and.returnValue(of<ActiveSubscription[]>([activeRow]));
    create = jasmine.createSpy('create').and.returnValue(of<SubscriptionResult>({ id: 'sub-9' }));
    cancel = jasmine.createSpy('cancel').and.returnValue(of<void>(undefined));
    pay = jasmine
      .createSpy('pay')
      .and.returnValue(of<PaySubscriptionResult>({ subscriptionId: 'sub-1' }));
    unpay = jasmine
      .createSpy('unpay')
      .and.returnValue(of<PaySubscriptionResult>({ subscriptionId: 'sub-1' }));
    TestBed.configureTestingModule({
      imports: [SubscriptionsPage],
      providers: [
        provideZonelessChangeDetection(),
        { provide: SubscriptionsService, useValue: { listActive, create, cancel, pay, unpay } },
        {
          provide: InstrumentsService,
          useValue: { list: () => of<Instrument[]>(instruments) }
        }
      ]
    });
  });

  it('creates', () => {
    setup();
    expect(fixture.componentInstance).toBeTruthy();
  });

  it('loads and renders the active list on init', () => {
    setup();
    expect(listActive).toHaveBeenCalledTimes(1);
    expect(view.listStatus()).toBe('ready');
    expect(view.active()).toEqual([activeRow]);
    expect(text()).toContain('Netflix');
  });

  it('renders the correct status badge per row', () => {
    listActive.and.returnValue(
      of<ActiveSubscription[]>([
        { ...activeRow, subscriptionId: 'sub-paid', status: 'paid' },
        { ...activeRow, subscriptionId: 'sub-overdue', status: 'overdue' },
        { ...activeRow, subscriptionId: 'sub-upcoming', status: 'upcoming' },
      ]),
    );
    setup();
    expect(text()).toContain('Paid');
    expect(text()).toContain('Overdue');
    expect(text()).toContain('Upcoming');
  });

  it('shows the empty state when there are no active subscriptions', () => {
    listActive.and.returnValue(of<ActiveSubscription[]>([]));
    setup();
    expect(text()).toContain('No active subscriptions.');
  });

  it('blocks submit while the form is invalid', () => {
    setup();
    view.form.setValue({
      name: '',
      amount: null,
      currency: 'ARS',
      category: '',
      fundingAccountId: '',
      frequency: 'monthly',
      anchorDay: null
    });
    view.onSubmit();
    expect(create).not.toHaveBeenCalled();
  });

  it('submits a minor-units body and re-fetches the list instead of inserting optimistically', () => {
    setup();
    listActive.calls.reset();
    view.form.setValue({
      name: '  Spotify  ',
      amount: 3000,
      currency: 'ARS',
      category: '  Music  ',
      fundingAccountId: 'acc-1',
      frequency: 'monthly',
      anchorDay: 1
    });
    view.onSubmit();
    expect(create).toHaveBeenCalledWith({
      name: 'Spotify',
      amountMinorUnits: 300000 as Money,
      category: 'Music',
      fundingAccountId: 'acc-1',
      frequency: 'monthly',
      anchorDay: 1,
      currencyCode: 'ARS',
    });
    expect(listActive).toHaveBeenCalledTimes(1);
    expect(view.submitStatus()).toBe('idle');
  });
  it('sends the selected USD currency code on submit', () => {
    setup();
    view.form.setValue({
      name: 'Netflix',
      amount: 15,
      currency: 'USD',
      category: 'Entertainment',
      fundingAccountId: 'acc-1',
      frequency: 'monthly',
      anchorDay: 1
    });
    view.onSubmit();
    expect(create).toHaveBeenCalledWith(jasmine.objectContaining({ currencyCode: 'USD' }));
  });
  it('renders a USD subscription amount in dollar formatting', () => {
    listActive.and.returnValue(of<ActiveSubscription[]>([{ ...activeRow, currencyCode: 'USD' }]));
    setup();
    expect(text()).toContain('$5,000.00');
  });
  it('renders submitErrorText keyed off the AppError code on a 422', () => {
    const appError: AppError = {
      code: 'Subscriptions.NonPositiveAmount',
      title: 'Unprocessable entity',
      detail: 'x',
      status: 422,
      metadata: {}
    };
    create.and.returnValue(throwError(() => appError));
    setup();
    view.form.setValue({
      name: 'Spotify',
      amount: 3000,
      currency: 'ARS',
      category: 'Music',
      fundingAccountId: 'acc-1',
      frequency: 'monthly',
      anchorDay: 1
    });
    view.onSubmit();
    fixture.detectChanges();
    expect(view.submitStatus()).toBe('error');
    expect(view.submitError()).toEqual(appError);
    expect(text()).toContain('The amount must be greater than zero.');
  });
  it('cancels a row via the service and reflects the re-fetched list (no optimistic removal)', () => {
    setup();
    listActive.calls.reset();
    listActive.and.returnValue(of<ActiveSubscription[]>([]));
    view.onCancel('sub-1');
    expect(cancel).toHaveBeenCalledWith('sub-1');
    expect(listActive).toHaveBeenCalledTimes(1);
    expect(view.active()).toEqual([]);
  });
  it('renders a per-row cancel error keyed off the AppError code on a 409', () => {
    const appError: AppError = {
      code: 'Subscriptions.SubscriptionNotActive',
      title: 'Conflict',
      detail: 'x',
      status: 409,
      metadata: {}
    };
    cancel.and.returnValue(throwError(() => appError));
    setup();
    view.onCancel('sub-1');
    fixture.detectChanges();
    expect(view.cancelStatusFor('sub-1')).toBe('error');
    expect(view.cancelErrorFor('sub-1')).toEqual(appError);
    expect(text()).toContain('That subscription was already cancelled.');
  });
  it('shows the Pay button only for overdue and upcoming rows, never for paid', () => {
    listActive.and.returnValue(
      of<ActiveSubscription[]>([
        { ...activeRow, subscriptionId: 'sub-paid', status: 'paid' },
        { ...activeRow, subscriptionId: 'sub-overdue', status: 'overdue' },
        { ...activeRow, subscriptionId: 'sub-upcoming', status: 'upcoming' },
      ]),
    );
    setup();
    expect(buttonsByLabel('Pay').length).toBe(2);
  });
  it('pays a row via the service and reflects the re-fetched list (no optimistic update)', () => {
    setup();
    listActive.calls.reset();
    listActive.and.returnValue(of<ActiveSubscription[]>([{ ...activeRow, status: 'paid' }]));
    view.onPay('sub-1');
    expect(pay).toHaveBeenCalledWith('sub-1');
    expect(listActive).toHaveBeenCalledTimes(1);
    expect(view.active()).toEqual([{ ...activeRow, status: 'paid' }]);
  });
  it('renders a per-row pay error keyed off the AppError code on a 409', () => {
    const appError: AppError = {
      code: 'Subscriptions.SubscriptionAlreadyPaid',
      title: 'Conflict',
      detail: 'x',
      status: 409,
      metadata: {}
    };
    pay.and.returnValue(throwError(() => appError));
    setup();
    view.onPay('sub-1');
    fixture.detectChanges();
    expect(view.payStatusFor('sub-1')).toBe('error');
    expect(view.payErrorFor('sub-1')).toEqual(appError);
    expect(text()).toContain('This period was already paid — the list was refreshed.');
  });
  it('shows the Undo button only for paid rows, never for overdue or upcoming', () => {
    listActive.and.returnValue(
      of<ActiveSubscription[]>([
        { ...activeRow, subscriptionId: 'sub-paid', status: 'paid' },
        { ...activeRow, subscriptionId: 'sub-overdue', status: 'overdue' },
        { ...activeRow, subscriptionId: 'sub-upcoming', status: 'upcoming' },
      ]),
    );
    setup();
    expect(buttonsByLabel('Undo').length).toBe(1);
  });
  it('undoes a row via the service and reflects the re-fetched list (no optimistic update)', () => {
    setup();
    listActive.calls.reset();
    listActive.and.returnValue(of<ActiveSubscription[]>([{ ...activeRow, status: 'overdue' }]));
    view.onUndo('sub-1');
    expect(unpay).toHaveBeenCalledWith('sub-1');
    expect(listActive).toHaveBeenCalledTimes(1);
    expect(view.active()).toEqual([{ ...activeRow, status: 'overdue' }]);
  });
  it('renders a per-row undo error keyed off the AppError code on a 409', () => {
    const appError: AppError = {
      code: 'Subscriptions.SubscriptionNotPaid',
      title: 'Conflict',
      detail: 'x',
      status: 409,
      metadata: {}
    };
    unpay.and.returnValue(throwError(() => appError));
    listActive.and.returnValue(of<ActiveSubscription[]>([{ ...activeRow, status: 'paid' }]));
    setup();
    view.onUndo('sub-1');
    fixture.detectChanges();
    expect(view.undoStatusFor('sub-1')).toBe('error');
    expect(view.undoErrorFor('sub-1')).toEqual(appError);
    expect(text()).toContain('This period is no longer paid — the list was refreshed.');
  });
});
