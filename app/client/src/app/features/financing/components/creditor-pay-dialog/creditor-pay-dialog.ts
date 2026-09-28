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
import { PayCreditorInstallment } from '../../types/pay-creditor-installment';
import { atMostTwoDecimals, positiveAmount } from '../../validation-helpers';

type PayChoice = 'full' | 'custom';
type PayMode = 'installment' | 'expense' | 'full-debt';

type PayDialogForm = FormGroup<{
  choice: FormControl<PayChoice>;
  amount: FormControl<number | null>;
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
  readonly busy: InputSignal<boolean> = input<boolean>(false);
  readonly error: InputSignal<string | null> = input<string | null>(null);
  readonly confirm: OutputEmitterRef<PayCreditorInstallment> = output<PayCreditorInstallment>();
  // eslint-disable-next-line @angular-eslint/no-output-native -- the slice doc names this output `cancel`
  readonly cancel: OutputEmitterRef<void> = output<void>();

  protected form!: PayDialogForm;
  protected readonly formatMoney: (value: Money, code: CurrencyCode) => string = formatMoney;
  protected readonly choice: WritableSignal<PayChoice> = signal<PayChoice>('full');
  protected readonly amountInvalid: WritableSignal<boolean> = signal<boolean>(false);
  protected readonly exceedsRemaining: Signal<boolean> = computed(() => {
    if(this.choice() !== 'custom') {
      return false;
    }
    const major: number | null = this.customAmountMajor();
    if(major === null || !Number.isFinite(major) || major <= 0) {
      return false;
    }
    try {
      return toMinorUnits(major) > this.remainingMinorUnits();
    } catch {
      return false;
    }
  });
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
      this.confirm.emit({ amountMinorUnits: null });
      return;
    }
    const major: number | null = this.form.controls.amount.value;
    if(major === null) {
      return;
    }
    this.confirm.emit({ amountMinorUnits: toMinorUnits(major) });
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
    });
  }

  private resetForm(): void {
    this.form.reset({ choice: 'full', amount: null });
    this.choice.set('full');
    this.customAmountMajor.set(null);
    this.amountInvalid.set(false);
  }

  ngOnInit(): void {
    this.initForm();
    this.form.controls.choice.valueChanges.pipe(takeUntil(this.destroy$)).subscribe((value: PayChoice) => {
      this.choice.set(value);
    });
    merge(this.form.controls.amount.valueChanges, this.form.controls.amount.statusChanges)
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => {
        this.customAmountMajor.set(this.form.controls.amount.value);
        this.amountInvalid.set(this.form.controls.amount.invalid);
      });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }
}
