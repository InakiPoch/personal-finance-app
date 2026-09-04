import {
  ChangeDetectionStrategy,
  Component,
  OnDestroy,
  OnInit,
  Signal,
  WritableSignal,
  computed,
  inject,
  signal,
} from '@angular/core';
import { FormBuilder, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Subject, takeUntil } from 'rxjs';
import { formatArs, toMinorUnits } from '../../../../core/money/money';
import { AppError } from '../../../../core/types/app-error';
import { Money } from '../../../../core/types/money';
import { InstrumentsService } from '../../../instruments/instruments-service';
import { Instrument } from '../../../instruments/types/instrument';
import { SubscriptionsService } from '../../subscriptions-service';
import { ActiveSubscription } from '../../types/active-subscription';
import { CreateSubscription } from '../../types/create-subscription';
import { Frequency } from '../../types/frequency';
import { atMostTwoDecimals, dayOfMonth, positiveAmount } from '../../validation-helpers';

type ListStatus = 'loading' | 'ready' | 'error';
type SubmitStatus = 'idle' | 'submitting' | 'error';
type RowCancelStatus = 'idle' | 'cancelling' | 'error';

type SubscriptionForm = FormGroup<{
  name: FormControl<string>;
  amount: FormControl<number | null>;
  category: FormControl<string>;
  fundingAccountId: FormControl<string>;
  frequency: FormControl<Frequency>;
  anchorDay: FormControl<number | null>;
}>;

@Component({
  selector: 'app-subscriptions-page',
  imports: [ReactiveFormsModule],
  templateUrl: './subscriptions-page.html',
  styleUrl: './subscriptions-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SubscriptionsPage implements OnInit, OnDestroy {
  protected form!: SubscriptionForm;
  protected readonly formatArs: (value: Money) => string = formatArs;
  protected readonly fundingAccounts: Signal<Instrument[]> = computed(() =>
    this.instruments()
  );
  protected readonly listStatus: WritableSignal<ListStatus> = signal<ListStatus>('loading');
  protected readonly active: WritableSignal<ActiveSubscription[]> = signal<ActiveSubscription[]>([]);
  protected readonly submitStatus: WritableSignal<SubmitStatus> = signal<SubmitStatus>('idle');
  protected readonly submitError: WritableSignal<AppError | null> = signal<AppError | null>(null);
  protected readonly cancelStatus: WritableSignal<Record<string, RowCancelStatus>> = signal<
    Record<string, RowCancelStatus>
  >({});
  protected readonly cancelError: WritableSignal<Record<string, AppError>> = signal<
    Record<string, AppError>
  >({});
  protected readonly fieldErrors: Record<string, string> = {
    required: 'This field is required.',
    positiveAmount: 'Enter an amount greater than zero.',
    atMostTwoDecimals: 'Use at most two decimal places.',
    dayOfMonth: 'Enter a whole day between 1 and 31.'
  };

  private readonly fb: FormBuilder = inject(FormBuilder);
  private readonly subscriptions: SubscriptionsService = inject(SubscriptionsService);
  private readonly instrumentsService: InstrumentsService = inject(InstrumentsService);
  private readonly instruments: WritableSignal<Instrument[]> = signal<Instrument[]>([]);
  private readonly submitErrorMessages: Record<string, string> = {
    'Subscriptions.InvalidName': 'Enter a name for the subscription.',
    'Subscriptions.InvalidCategory': 'Enter a category.',
    'Subscriptions.NonPositiveAmount': 'The amount must be greater than zero.',
    'Subscriptions.InvalidAnchorDay': 'The anchor day must be between 1 and 31.',
    'Subscriptions.InvalidFundingAccount': 'Choose a funding account registered with the API.',
    'Http.BadRequest': 'The subscription could not be created — check the values and try again.',
    'Http.UnprocessableEntity': 'The API rejected the subscription — check the amount and anchor day.',
    'Http.ServerError': 'Something went wrong on the server. Try again in a moment.',
    'Http.NetworkError': 'Could not reach the server. Check your connection.'
  };
  private readonly cancelErrorMessages: Record<string, string> = {
    'Subscriptions.SubscriptionNotFound': 'That subscription no longer exists — the list was refreshed.',
    'Subscriptions.SubscriptionNotActive': 'That subscription was already cancelled.',
    'Http.NotFound': 'That subscription no longer exists — the list was refreshed.',
    'Http.Conflict': 'That subscription was already cancelled.',
    'Http.ServerError': 'Something went wrong on the server. Try again in a moment.',
    'Http.NetworkError': 'Could not reach the server. Check your connection.'
  };
  private readonly destroy$: Subject<void> = new Subject<void>();

  protected onSubmit(): void {
    if(this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const raw: {
      name: string;
      amount: number | null;
      category: string;
      fundingAccountId: string;
      frequency: Frequency;
      anchorDay: number | null;
    } = this.form.getRawValue();
    const body: CreateSubscription = {
      name: raw.name.trim(),
      amountMinorUnits: toMinorUnits(raw.amount as number),
      category: raw.category.trim(),
      fundingAccountId: raw.fundingAccountId,
      frequency: raw.frequency,
      anchorDay: raw.anchorDay as number
    };
    this.submitError.set(null);
    this.submitStatus.set('submitting');
    this.subscriptions
      .create(body)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.form.reset({
            name: '',
            amount: null,
            category: '',
            fundingAccountId: '',
            frequency: 'monthly',
            anchorDay: null,
          });
          this.submitStatus.set('idle');
          this.loadActive();
        },
        error: (error: AppError) => {
          this.submitError.set(error);
          this.submitStatus.set('error');
        }
      }
    );
  }

  protected onCancel(subscriptionId: string): void {
    this.setCancelStatus(subscriptionId, 'cancelling');
    this.clearCancelError(subscriptionId);
    this.subscriptions
      .cancel(subscriptionId)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => this.loadActive(),
        error: (error: AppError) => {
          this.setCancelStatus(subscriptionId, 'error');
          this.cancelError.update((map: Record<string, AppError>) => ({
            ...map,
            [subscriptionId]: error,
          }));
        }
      }
    );
  }

  protected cancelStatusFor(subscriptionId: string): RowCancelStatus {
    return this.cancelStatus()[subscriptionId] ?? 'idle';
  }

  protected cancelErrorFor(subscriptionId: string): AppError | null {
    return this.cancelError()[subscriptionId] ?? null;
  }

  protected submitErrorText(error: AppError): string {
    return this.submitErrorMessages[error.code] ?? 'The subscription could not be created.';
  }

  protected cancelErrorText(error: AppError): string {
    return this.cancelErrorMessages[error.code] ?? 'The subscription could not be cancelled.';
  }

  private loadInstruments(): void {
    this.instrumentsService
      .list()
      .pipe(takeUntil(this.destroy$))
      .subscribe((rows: Instrument[]) => this.instruments.set(rows));
  }

  private loadActive(): void {
    this.listStatus.set('loading');
    this.subscriptions
      .listActive()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (rows: ActiveSubscription[]) => {
          this.active.set(rows);
          this.listStatus.set('ready');
        },
        error: () => this.listStatus.set('error'),
      }
    );
  }

  private setCancelStatus(subscriptionId: string, status: RowCancelStatus): void {
    this.cancelStatus.update((map: Record<string, RowCancelStatus>) => ({
      ...map,
      [subscriptionId]: status
    }));
  }

  private clearCancelError(subscriptionId: string): void {
    this.cancelError.update((map: Record<string, AppError>) => {
      const next: Record<string, AppError> = { ...map };
      delete next[subscriptionId];
      return next;
    });
  }

  private initSubscriptionForm(): void {
    this.form = this.fb.group({
      name: this.fb.nonNullable.control('', { validators: Validators.required }),
      amount: this.fb.control<number | null>(null, {
        validators: [positiveAmount, atMostTwoDecimals],
      }),
      category: this.fb.nonNullable.control('', { validators: Validators.required }),
      fundingAccountId: this.fb.nonNullable.control('', { validators: Validators.required }),
      frequency: this.fb.nonNullable.control<Frequency>({ value: 'monthly', disabled: true }),
      anchorDay: this.fb.control<number | null>(null, { validators: dayOfMonth })
    });
  }

  ngOnInit(): void {
    this.initSubscriptionForm();
    this.loadInstruments();
    this.loadActive();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }
}
