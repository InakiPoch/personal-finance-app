import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  InputSignal,
  OnDestroy,
  OnInit,
  OutputEmitterRef,
  Signal,
  WritableSignal,
  computed,
  effect,
  inject,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { FormBuilder, FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { Subject, merge, takeUntil } from 'rxjs';
import { formatMoney, toMinorUnits } from '../../../../core/money/money';
import { CurrencyCode } from '../../../../core/types/currency-code';
import { Money } from '../../../../core/types/money';
import { CreditorOutstandingByCurrency } from '../../types/creditor-outstanding-by-currency';
import { PayCreditorFullDebt } from '../../types/pay-creditor-full-debt';
import { atMostTwoDecimals, positiveAmount } from '../../validation-helpers';

type PayChoice = 'full' | 'custom';
type PayMode = 'installment' | 'expense' | 'full-debt';

type PayDialogForm = FormGroup<{
  choice: FormControl<PayChoice>;
  amount: FormControl<number | null>;
  currencyCode: FormControl<CurrencyCode>;
}>;

@Component({
  selector: 'app-creditor-pay-dialog',
  imports: [ReactiveFormsModule],
  templateUrl: './creditor-pay-dialog.html',
  styleUrl: './creditor-pay-dialog.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CreditorPayDialog implements OnInit, OnDestroy {
  readonly open: InputSignal<boolean> = input<boolean>(false);
  readonly mode: InputSignal<PayMode> = input<PayMode>('installment');
  readonly title: InputSignal<string> = input.required<string>();
  readonly remainingMinorUnits: InputSignal<Money> = input.required<Money>();
  readonly currency: InputSignal<CurrencyCode> = input.required<CurrencyCode>();
  readonly currencies: InputSignal<CreditorOutstandingByCurrency[]> = input<CreditorOutstandingByCurrency[]>([]);
  readonly busy: InputSignal<boolean> = input<boolean>(false);
  readonly error: InputSignal<string | null> = input<string | null>(null);
  readonly confirm: OutputEmitterRef<PayCreditorFullDebt> = output<PayCreditorFullDebt>();
  // eslint-disable-next-line @angular-eslint/no-output-native -- the slice doc names this output `cancel`
  readonly cancel: OutputEmitterRef<void> = output<void>();

  protected form!: PayDialogForm;
  protected readonly formatMoney: (value: Money, code: CurrencyCode) => string = formatMoney;
  protected readonly choice: WritableSignal<PayChoice> = signal<PayChoice>('full');
  protected readonly amountInvalid: WritableSignal<boolean> = signal<boolean>(false);
  protected readonly showCurrencySelect: Signal<boolean> = computed(() =>
    this.mode() === 'full-debt' && this.currencies().length > 1
  );
  protected readonly effectiveCurrency: Signal<CurrencyCode> = computed(() => {
    if(!this.showCurrencySelect()) {
      return this.currency();
    }
    return this.selectedCurrencyCode() ?? this.currencies()[0]?.currencyCode ?? this.currency();
  });
  protected readonly effectiveRemaining: Signal<Money> = computed(() => {
    if(!this.showCurrencySelect()) {
      return this.remainingMinorUnits();
    }
    const match: CreditorOutstandingByCurrency | undefined = this.currencies()
      .find((entry: CreditorOutstandingByCurrency) => entry.currencyCode === this.effectiveCurrency());
    return match?.outstandingMinorUnits ?? this.remainingMinorUnits();
  });
  protected readonly payInFullSummary: Signal<string> = computed(() => {
    if(this.mode() === 'full-debt' && this.currencies().length > 1) {
      return this.currencies()
        .map((entry: CreditorOutstandingByCurrency) => this.formatMoney(entry.outstandingMinorUnits, entry.currencyCode))
      .join(' + ');
    }
    return this.formatMoney(this.remainingMinorUnits(), this.currency());
  });
  protected readonly exceedsRemaining: Signal<boolean> = computed(() => {
    if(this.choice() !== 'custom') {
      return false;
    }
    const major: number | null = this.customAmountMajor();
    if(major === null || !Number.isFinite(major) || major <= 0) {
      return false;
    }
    try {
      return toMinorUnits(major) > this.effectiveRemaining();
    } catch {
      return false;
    }
  });
  protected readonly payInFullLabel: Signal<string> = computed(() =>
    this.mode() === 'expense' ? 'Pay the whole expense' : 'Pay in full'
  );
  protected readonly payCustomLabel: Signal<string> = computed(() =>
    this.mode() === 'expense' ? 'Pay part of it' : 'Pay a custom amount'
  );
  protected readonly confirmDisabled: Signal<boolean> = computed(() => {
    if(this.busy()) {
      return true;
    }
    if(this.choice() === 'full') {
      return false;
    }
    return this.amountInvalid() || this.exceedsRemaining();
  });

  private readonly fb: FormBuilder = inject(FormBuilder);
  private readonly dialogEl: Signal<ElementRef<HTMLDialogElement> | undefined> =
    viewChild<ElementRef<HTMLDialogElement>>('dialogEl');
  private readonly customAmountMajor: WritableSignal<number | null> = signal<number | null>(null);
  private readonly selectedCurrencyCode: WritableSignal<CurrencyCode | null> = signal<CurrencyCode | null>(null);
  private readonly destroy$: Subject<void> = new Subject<void>();

  constructor() {
    effect(() => {
      const dialog: HTMLDialogElement | undefined = this.dialogEl()?.nativeElement;
      if(!dialog) {
        return;
      }
      if(this.open()) {
        this.resetForm();
        if(!dialog.open) {
          dialog.showModal();
        }
      } else if(dialog.open) {
        dialog.close();
      }
    });
  }

  protected onConfirm(): void {
    if(this.confirmDisabled()) {
      return;
    }
    if(this.choice() === 'full') {
      this.confirm.emit({ amountMinorUnits: null, currencyCode: null });
      return;
    }
    const major: number | null = this.form.controls.amount.value;
    if(major === null) {
      return;
    }
    const currencyCode: CurrencyCode | null = this.mode() === 'full-debt' ? this.effectiveCurrency() : null;
    this.confirm.emit({ amountMinorUnits: toMinorUnits(major), currencyCode });
  }

  protected onCancel(): void {
    this.cancel.emit();
  }

  protected onBackdropClick(event: MouseEvent): void {
    if(event.target === event.currentTarget) {
      this.onCancel();
    }
  }

  private initForm(): void {
    this.form = this.fb.group({
      choice: this.fb.nonNullable.control<PayChoice>('full'),
      amount: this.fb.control<number | null>(null, [positiveAmount, atMostTwoDecimals]),
      currencyCode: this.fb.nonNullable.control<CurrencyCode>(this.defaultCurrencyCode()),
    });
  }

  private resetForm(): void {
    const defaultCurrencyCode: CurrencyCode = this.defaultCurrencyCode();
    this.form.reset({ choice: 'full', amount: null, currencyCode: defaultCurrencyCode });
    this.choice.set('full');
    this.customAmountMajor.set(null);
    this.selectedCurrencyCode.set(defaultCurrencyCode);
    this.amountInvalid.set(false);
  }

  private defaultCurrencyCode(): CurrencyCode {
    return this.currencies()[0]?.currencyCode ?? this.currency();
  }

  ngOnInit(): void {
    this.initForm();
    this.form.controls.choice.valueChanges.pipe(takeUntil(this.destroy$)).subscribe((value: PayChoice) => {
      this.choice.set(value);
    });
    this.form.controls.currencyCode.valueChanges.pipe(takeUntil(this.destroy$)).subscribe((value: CurrencyCode) => {
      this.selectedCurrencyCode.set(value);
    });
    merge(this.form.controls.amount.valueChanges, this.form.controls.amount.statusChanges)
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => {
        this.customAmountMajor.set(this.form.controls.amount.value);
        this.amountInvalid.set(this.form.controls.amount.invalid);
      }
    );
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }
}
