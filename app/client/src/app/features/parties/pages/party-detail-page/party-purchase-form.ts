import {
  ChangeDetectionStrategy,
  Component,
  InputSignal,
  OnDestroy,
  OnInit,
  OutputEmitterRef,
  WritableSignal,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { FormBuilder, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Subject, takeUntil } from 'rxjs';
import { toMinorUnits } from '../../../../core/money/money';
import { AppError } from '../../../../core/types/app-error';
import { CurrencyCode } from '../../../../core/types/currency-code';
import { IsoDate } from '../../../../core/types/iso-date';
import { LedgerService } from '../../../ledger/ledger-service';
import { PartiesService } from '../../parties-service';
import { PartyPurchaseKind } from '../../types/party-purchase-kind';
import { RecordPartyPurchase } from '../../types/record-party-purchase';
import { atMostTwoDecimals, notFutureDate, positiveAmount, singleLine } from '../../validation-helpers';

type PurchaseStatus = 'idle' | 'saving' | 'saved' | 'error';

type PurchaseForm = FormGroup<{
  kind: FormControl<PartyPurchaseKind>;
  share: FormControl<number | null>;
  currency: FormControl<CurrencyCode>;
  categoryName: FormControl<string>;
  description: FormControl<string>;
  purchaseDate: FormControl<string>;
}>;

@Component({
  selector: 'app-party-purchase-form',
  imports: [ReactiveFormsModule],
  templateUrl: './party-purchase-form.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class PartyPurchaseForm implements OnInit, OnDestroy {
  readonly partyId: InputSignal<string> = input.required<string>();
  readonly partyName: InputSignal<string> = input<string>('this party');
  readonly recorded: OutputEmitterRef<void> = output<void>();

  protected form!: PurchaseForm;
  protected readonly status: WritableSignal<PurchaseStatus> = signal<PurchaseStatus>('idle');
  protected readonly error: WritableSignal<AppError | null> = signal<AppError | null>(null);
  protected readonly categories: WritableSignal<string[]> = signal<string[]>([]);
  protected readonly currencies: readonly CurrencyCode[] = ['ARS', 'USD'];
  protected readonly fieldErrors: Record<string, string> = {
    required: 'This field is required.',
    positiveAmount: 'Enter an amount greater than zero.',
    atMostTwoDecimals: 'Use at most two decimal places.',
    maxlength: 'Use at most 120 characters.',
    singleLine: 'Keep the description on one line.',
    notFutureDate: 'The purchase date cannot be in the future.'
  };

  private readonly fb: FormBuilder = inject(FormBuilder);
  private readonly partiesService: PartiesService = inject(PartiesService);
  private readonly ledgerService: LedgerService = inject(LedgerService);
  private readonly errorMessages: Record<string, string> = {
    'Parties.PartyNotFound': 'This party no longer exists.',
    'Parties.NonPositiveAmount': 'Your share must be greater than zero.',
    'Parties.InvalidCurrencyCode': 'Choose ARS or USD.',
    'Parties.InvalidPurchaseDescription': 'Add a one-line description of up to 120 characters.',
    'Parties.InvalidPurchaseCategory': 'Enter a category for the purchase.',
    'Parties.InvalidPurchaseKind': 'This kind of purchase is not available yet.',
    'Parties.PurchaseDateInFuture': 'The purchase date cannot be in the future.',
    'Http.UnprocessableEntity': 'Check the share, category, date and description and try again.',
    'Http.ServerError': 'Something went wrong on the server. Try again in a moment.',
    'Http.NetworkError': 'Could not reach the server. Check your connection.'
  };
  private readonly destroy$: Subject<void> = new Subject<void>();

  protected errorText(error: AppError): string {
    return this.errorMessages[error.code] ?? 'The purchase could not be recorded.';
  }

  protected onSubmit(): void {
    if(this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const raw: {
      kind: PartyPurchaseKind;
      share: number | null;
      currency: CurrencyCode;
      categoryName: string;
      description: string;
      purchaseDate: string;
    } = this.form.getRawValue();
    const body: RecordPartyPurchase = {
      shareMinorUnits: toMinorUnits(raw.share as number),
      currencyCode: raw.currency,
      description: raw.description.trim(),
      categoryName: raw.categoryName.trim(),
      purchaseDate: raw.purchaseDate as IsoDate,
      kind: raw.kind,
      today: new Date().toLocaleDateString('sv-SE') as IsoDate
    };
    this.error.set(null);
    this.status.set('saving');
    this.partiesService
      .recordPartyPurchase(this.partyId(), body)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.status.set('saved');
          this.form.reset({ kind: 'debit', share: null, currency: 'ARS', categoryName: '', description: '', purchaseDate: '' });
          this.recorded.emit();
        },
        error: (error: AppError) => {
          this.error.set(error);
          this.status.set('error');
        }
      }
    );
  }

  private initForm(): void {
    this.form = this.fb.group({
      kind: this.fb.nonNullable.control<PartyPurchaseKind>('debit'),
      share: this.fb.control<number | null>(null, { validators: [positiveAmount, atMostTwoDecimals] }),
      currency: this.fb.nonNullable.control<CurrencyCode>('ARS'),
      categoryName: this.fb.nonNullable.control('', { validators: [Validators.required, Validators.pattern(/\S/)] }),
      description: this.fb.nonNullable.control('', {
        validators: [Validators.required, Validators.pattern(/\S/), Validators.maxLength(120), singleLine]
      }),
      purchaseDate: this.fb.nonNullable.control('', { validators: [Validators.required, notFutureDate] })
    });
  }

  ngOnInit(): void {
    this.initForm();
    this.ledgerService
      .listExpenseCategories()
      .pipe(takeUntil(this.destroy$))
      .subscribe((names: string[]) => this.categories.set(names));
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }
}
