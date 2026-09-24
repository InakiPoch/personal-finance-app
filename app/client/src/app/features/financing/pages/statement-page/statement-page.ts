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
import { ActivatedRoute, ParamMap, Router } from '@angular/router';
import { Subject, takeUntil } from 'rxjs';
import { formatMoney } from '../../../../core/money/money';
import { AppError } from '../../../../core/types/app-error';
import { CurrencyCode } from '../../../../core/types/currency-code';
import { IsoInstant } from '../../../../core/types/iso-instant';
import { Money } from '../../../../core/types/money';
import { InstrumentsService } from '../../../instruments/instruments-service';
import { Instrument } from '../../../instruments/types/instrument';
import { FinancingService } from '../../financing-service';
import { MonthlyStatement } from '../../types/monthly-statement';
import { PayInstallment } from '../../types/pay-installment';
import { PayStatement } from '../../types/pay-statement';
import { PayStatementResult } from '../../types/pay-statement-result';
import { InstallmentsTable } from './installments-table';

type LoadStatus = 'loading' | 'ready' | 'error';
type PayStatus = 'idle' | 'paying' | 'paid' | 'error';

type PayForm = FormGroup<{
  bankAccountId: FormControl<string>;
  paidOnUtc: FormControl<string>;
}>;

@Component({
  selector: 'app-statement-page',
  imports: [ReactiveFormsModule, InstallmentsTable],
  templateUrl: './statement-page.html',
  styleUrl: './statement-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StatementPage implements OnInit, OnDestroy {
  protected form!: PayForm;
  protected readonly formatMoney: (value: Money, code: CurrencyCode) => string = formatMoney;
  protected readonly statement: WritableSignal<MonthlyStatement | null> =
    signal<MonthlyStatement | null>(null);
  protected readonly loadStatus: WritableSignal<LoadStatus> = signal<LoadStatus>('loading');
  protected readonly payStatus: WritableSignal<PayStatus> = signal<PayStatus>('idle');
  protected readonly payError: WritableSignal<AppError | null> = signal<AppError | null>(null);
  protected readonly bankAccounts: Signal<Instrument[]> = computed(() =>
    this.instruments().filter((instrument: Instrument) => instrument.type === 'debit')
  );
  protected readonly fieldErrors: Record<string, string> = {
    required: 'This field is required.'
  };

  private readonly route: ActivatedRoute = inject(ActivatedRoute);
  private readonly router: Router = inject(Router);
  private readonly fb: FormBuilder = inject(FormBuilder);
  private readonly financing: FinancingService = inject(FinancingService);
  private readonly instrumentsService: InstrumentsService = inject(InstrumentsService);
  private readonly instruments: WritableSignal<Instrument[]> = signal<Instrument[]>([]);
  private readonly payErrorMessages: Record<string, string> = {
    'Financing.AlreadyPaid': 'This statement has already been paid.',
    'Financing.StatementNotFound': 'No statement matches that id.',
    'Financing.InstallmentNotFound': 'No installment matches that id.',
    'Financing.InstallmentAlreadyPaid': 'That installment has already been paid.',
    'Financing.InstallmentAlreadyReversed': 'That installment was reversed and cannot be paid.',
    'Financing.InstallmentNotAccrued': 'That installment has not been billed to a statement yet.',
    'Http.BadRequest': 'The payment could not be recorded — check the values and try again.',
    'Http.UnprocessableEntity': 'The API rejected the payment — check the account and date.',
    'Http.Conflict': 'This statement has already been paid.',
    'Http.ServerError': 'Something went wrong on the server. Try again in a moment.',
    'Http.NetworkError': 'Could not reach the server. Check your connection.'
  };
  private statementId: string | null = null;
  private readonly destroy$: Subject<void> = new Subject<void>();

  protected onSubmit(): void {
    if(this.form.invalid || this.statementId === null) {
      this.form.markAllAsTouched();
      return;
    }
    const raw: { bankAccountId: string; paidOnUtc: string } = this.form.getRawValue();
    const body: PayStatement = {
      bankAccountId: raw.bankAccountId,
      paidOnUtc: new Date(raw.paidOnUtc).toISOString() as IsoInstant
    };
    this.payError.set(null);
    this.payStatus.set('paying');
    this.financing
      .payStatement(this.statementId, body)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (result: PayStatementResult) => {
          this.payStatus.set('paid');
          this.loadStatement(result.statementId);
        },
        error: (error: AppError) => {
          this.payError.set(error);
          this.payStatus.set('error');
        }
      }
    );
  }

  protected onPayInstallment(installmentId: string): void {
    if(this.form.invalid || this.statementId === null) {
      this.form.markAllAsTouched();
      return;
    }
    const statementId: string = this.statementId;
    const raw: { bankAccountId: string; paidOnUtc: string } = this.form.getRawValue();
    const body: PayInstallment = {
      bankAccountId: raw.bankAccountId,
      paidOnUtc: new Date(raw.paidOnUtc).toISOString() as IsoInstant
    };
    this.payError.set(null);
    this.payStatus.set('paying');
    this.financing
      .payInstallment(installmentId, body)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.payStatus.set('paid');
          this.loadStatement(statementId);
        },
        error: (error: AppError) => {
          this.payError.set(error);
          this.payStatus.set('error');
        }
      }
    );
  }

  protected payErrorText(error: AppError): string {
    return this.payErrorMessages[error.code] ?? 'The payment could not be recorded.';
  }

  protected openReverse(transactionId: string): void {
    this.router.navigate(['ledger', 'transactions', transactionId, 'reverse']);
  }

  private loadInstruments(): void {
    this.instrumentsService
      .list()
      .pipe(takeUntil(this.destroy$))
    .subscribe((rows: Instrument[]) => this.instruments.set(rows));
  }

  private loadStatement(id: string): void {
    this.loadStatus.set('loading');
    this.financing
      .getStatement(id)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (statement: MonthlyStatement) => {
          this.statement.set(statement);
          this.loadStatus.set('ready');
        },
        error: () => this.loadStatus.set('error')
      }
    );
  }

  private initPayForm(): void {
    this.form = this.fb.group({
      bankAccountId: this.fb.nonNullable.control('', { validators: Validators.required }),
      paidOnUtc: this.fb.nonNullable.control('', { validators: Validators.required })
    });
  }

  ngOnInit(): void {
    this.initPayForm();
    this.loadInstruments();
    this.route.paramMap.pipe(takeUntil(this.destroy$)).subscribe({
      next: (params: ParamMap) => {
        const id: string | null = params.get('id');
        this.statementId = id;
        if(id !== null) {
          this.loadStatement(id);
        }
      },
    });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }
}
