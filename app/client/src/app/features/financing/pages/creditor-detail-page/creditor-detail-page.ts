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
import { ActivatedRoute, ParamMap } from '@angular/router';
import { Observable, Subject, takeUntil } from 'rxjs';
import { fromMinorUnits } from '../../../../core/money/money';
import { AppError } from '../../../../core/types/app-error';
import { CurrencyCode } from '../../../../core/types/currency-code';
import { Money } from '../../../../core/types/money';
import { CreditorPayDialog } from '../../components/creditor-pay-dialog/creditor-pay-dialog';
import { FinancingService } from '../../financing-service';
import { CreditorDetail } from '../../types/creditor-detail';
import { CreditorInstallmentRow } from '../../types/creditor-installment-row';
import { CreditorPurchaseGroup } from '../../types/creditor-purchase-group';
import { PayCreditorExpenseResult } from '../../types/pay-creditor-expense-result';
import { PayCreditorFullDebtResult } from '../../types/pay-creditor-full-debt-result';
import { PayCreditorInstallment } from '../../types/pay-creditor-installment';
import { PayCreditorInstallmentResult } from '../../types/pay-creditor-installment-result';
import { CreditorPurchasesTable } from './creditor-purchases-table';

type LoadStatus = 'idle' | 'loading' | 'ready' | 'error';
type PayStatus = 'idle' | 'busy' | 'error';
type PayTarget =
  | { kind: 'installment'; row: CreditorInstallmentRow }
  | { kind: 'expense'; group: CreditorPurchaseGroup };

@Component({
  selector: 'app-creditor-detail-page',
  imports: [CreditorPurchasesTable, CreditorPayDialog],
  templateUrl: './creditor-detail-page.html',
  styleUrl: './creditor-detail-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CreditorDetailPage implements OnInit, OnDestroy {
  protected readonly detail: WritableSignal<CreditorDetail | null> = signal<CreditorDetail | null>(null);
  protected readonly loadStatus: WritableSignal<LoadStatus> = signal<LoadStatus>('idle');
  protected readonly loadError: WritableSignal<AppError | null> = signal<AppError | null>(null);
  protected readonly payStatus: WritableSignal<PayStatus> = signal<PayStatus>('idle');
  protected readonly payError: WritableSignal<AppError | null> = signal<AppError | null>(null);
  protected readonly confirmingFullDebt: WritableSignal<boolean> = signal<boolean>(false);
  protected readonly lastSettledCount: WritableSignal<number | null> = signal<number | null>(null);
  protected readonly payTarget: WritableSignal<PayTarget | null> = signal<PayTarget | null>(null);
  protected readonly payDialogMode: Signal<'installment' | 'expense'> = computed(() =>
    this.payTarget()?.kind ?? 'installment'
  );
  protected readonly payTargetRemaining: Signal<Money> = computed(() => {
    const target: PayTarget | null = this.payTarget();
    if(target === null) {
      return fromMinorUnits(0);
    }
    return target.kind === 'expense' ? target.group.outstandingMinorUnits : target.row.remainingMinorUnits;
  });
  protected readonly payTargetGroup: Signal<CreditorPurchaseGroup | null> = computed(() => {
    const target: PayTarget | null = this.payTarget();
    if(target === null) {
      return null;
    }
    if(target.kind === 'expense') {
      return target.group;
    }
    return (
      (this.detail()?.purchases ?? []).find((group: CreditorPurchaseGroup) =>
        group.installments.some((installment: CreditorInstallmentRow) => installment.installmentId === target.row.installmentId)
      ) ?? null
    );
  });
  protected readonly payDialogTitle: Signal<string> = computed(() => {
    const target: PayTarget | null = this.payTarget();
    if(target === null) {
      return '';
    }
    if(target.kind === 'expense') {
      return target.group.description;
    }
    return `Cuota ${target.row.sequence}/${target.row.installmentCount} · ${this.payTargetGroup()?.description ?? ''}`;
  });
  protected readonly payDialogCurrency: Signal<CurrencyCode> = computed(() =>
    this.payTargetGroup()?.currencyCode ?? 'ARS'
  );

  private readonly route: ActivatedRoute = inject(ActivatedRoute);
  private readonly financing: FinancingService = inject(FinancingService);
  private readonly destroy$: Subject<void> = new Subject<void>();
  private readonly payErrorMessages: Record<string, string> = {
    'Financing.NotACreditorInstallment': 'That installment is on a credit-card plan, not a creditor one.',
    'Financing.InstallmentAlreadyPaid': 'That installment is already marked paid.',
    'Financing.InstallmentAlreadyReversed': 'That installment was reversed and cannot be paid.',
    'Financing.InstallmentNotFound': 'No installment matches that id.',
    'Financing.InvalidPaymentAmount': 'Enter a valid payment amount.',
    'Financing.PaymentExceedsRemaining': "That payment exceeds what's still remaining.",
    'Financing.NoPaymentToUndo': 'There is no payment recorded on this installment to undo.'
  };
  private creditorId: string | null = null;

  protected isNotFound(): boolean {
    return this.loadError()?.code === 'Financing.CreditorNotFound';
  }

  protected payErrorText(error: AppError): string {
    return this.payErrorMessages[error.code] ?? 'That change could not be saved — try again in a moment.';
  }

  protected onPay(row: CreditorInstallmentRow): void {
    this.payError.set(null);
    this.payTarget.set({ kind: 'installment', row });
  }

  protected onPayExpense(group: CreditorPurchaseGroup): void {
    this.payError.set(null);
    this.payTarget.set({ kind: 'expense', group });
  }

  protected onDialogCancel(): void {
    this.payTarget.set(null);
    this.payError.set(null);
  }

  protected onDialogConfirm(body: PayCreditorInstallment): void {
    const target: PayTarget | null = this.payTarget();
    if(target === null) {
      return;
    }
    if(target.kind === 'expense') {
      this.runPayExpense(target.group.planId, body);
      return;
    }
    this.runPay(target.row.installmentId, body);
  }

  protected onUndo(installmentId: string): void {
    this.runMutation(this.financing.unpayCreditorInstallment(installmentId));
  }

  protected hasOutstanding(): boolean {
    return (this.detail()?.purchases ?? []).some(
      (group: CreditorPurchaseGroup) => group.outstandingMinorUnits > 0,
    );
  }

  protected requestPayFullDebt(): void {
    this.payError.set(null);
    this.lastSettledCount.set(null);
    this.confirmingFullDebt.set(true);
  }

  protected cancelPayFullDebt(): void {
    this.confirmingFullDebt.set(false);
  }

  protected confirmPayFullDebt(): void {
    const creditorId: string | null = this.creditorId;
    if(creditorId === null) {
      return;
    }
    this.payError.set(null);
    this.payStatus.set('busy');
    this.financing
      .payCreditorFullDebt(creditorId)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (result: PayCreditorFullDebtResult) => {
          this.payStatus.set('idle');
          this.confirmingFullDebt.set(false);
          this.lastSettledCount.set(result.settledCount);
          this.loadDetail(creditorId);
        },
        error: (error: AppError) => {
          this.payError.set(error);
          this.payStatus.set('error');
          this.confirmingFullDebt.set(false);
        }
      }
    );
  }

  private runPay(installmentId: string, body: PayCreditorInstallment): void {
    const creditorId: string | null = this.creditorId;
    if(creditorId === null) {
      return;
    }
    this.payError.set(null);
    this.payStatus.set('busy');
    this.financing
      .payCreditorInstallment(installmentId, body)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.payStatus.set('idle');
          this.payTarget.set(null);
          this.loadDetail(creditorId);
        },
        error: (error: AppError) => {
          this.payError.set(error);
          this.payStatus.set('error');
        }
      }
    );
  }

  private runPayExpense(planId: string, body: PayCreditorInstallment): void {
    const creditorId: string | null = this.creditorId;
    if(creditorId === null) {
      return;
    }
    this.payError.set(null);
    this.payStatus.set('busy');
    this.financing
      .payCreditorExpense(planId, body)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (result: PayCreditorExpenseResult) => {
          this.payStatus.set('idle');
          this.payTarget.set(null);
          this.lastSettledCount.set(result.settledCount);
          this.loadDetail(creditorId);
        },
        error: (error: AppError) => {
          this.payError.set(error);
          this.payStatus.set('error');
        }
      }
    );
  }

  private runMutation(operation: Observable<PayCreditorInstallmentResult>): void {
    const creditorId: string | null = this.creditorId;
    if(creditorId === null) {
      return;
    }
    this.payError.set(null);
    this.payStatus.set('busy');
    operation.pipe(takeUntil(this.destroy$)).subscribe({
      next: () => {
        this.payStatus.set('idle');
        this.loadDetail(creditorId);
      },
      error: (error: AppError) => {
        this.payError.set(error);
        this.payStatus.set('error');
      }
    });
  }

  private loadDetail(creditorId: string): void {
    this.loadStatus.set('loading');
    this.loadError.set(null);
    this.financing
      .creditorDetail(creditorId)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (detail: CreditorDetail) => {
          this.detail.set(detail);
          this.loadStatus.set('ready');
        },
        error: (error: AppError) => {
          this.loadError.set(error);
          this.loadStatus.set('error');
        }
      }
    );
  }

  ngOnInit(): void {
    this.route.paramMap.pipe(takeUntil(this.destroy$)).subscribe({
      next: (params: ParamMap) => {
        const id: string | null = params.get('creditorId');
        this.creditorId = id;
        if(id !== null) {
          this.loadDetail(id);
        }
      }
    });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }
}
