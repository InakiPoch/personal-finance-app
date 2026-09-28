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
import { InstrumentsService } from '../../../instruments/instruments-service';
import { Instrument } from '../../../instruments/types/instrument';
import { CreditorPayDialog } from '../../components/creditor-pay-dialog/creditor-pay-dialog';
import { FinancingService } from '../../financing-service';
import { CreditorDetail } from '../../types/creditor-detail';
import { CreditorInstallmentPartyShare } from '../../types/creditor-installment-party-share';
import { CreditorInstallmentRow } from '../../types/creditor-installment-row';
import { CreditorOutstandingByCurrency } from '../../types/creditor-outstanding-by-currency';
import { CreditorPayDialogConfirm } from '../../types/creditor-pay-dialog-confirm';
import { CreditorPurchaseGroup } from '../../types/creditor-purchase-group';
import { PayCreditorExpenseResult } from '../../types/pay-creditor-expense-result';
import { PayCreditorFullDebt } from '../../types/pay-creditor-full-debt';
import { PayCreditorFullDebtResult } from '../../types/pay-creditor-full-debt-result';
import { PayCreditorInstallment } from '../../types/pay-creditor-installment';
import { PayCreditorInstallmentPartyShare } from '../../types/pay-creditor-installment-party-share';
import { PayCreditorInstallmentResult } from '../../types/pay-creditor-installment-result';
import { CreditorPurchasesTable } from './creditor-purchases-table';

type LoadStatus = 'idle' | 'loading' | 'ready' | 'error';
type PayStatus = 'idle' | 'busy' | 'error';
type PayTarget =
  | { kind: 'installment'; row: CreditorInstallmentRow }
  | { kind: 'expense'; group: CreditorPurchaseGroup }
  | { kind: 'full-debt' };

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
  protected readonly lastSettledCount: WritableSignal<number | null> = signal<number | null>(null);
  protected readonly payTarget: WritableSignal<PayTarget | null> = signal<PayTarget | null>(null);
  protected readonly payDialogMode: Signal<'installment' | 'expense' | 'full-debt'> = computed(() =>
    this.payTarget()?.kind ?? 'installment'
  );
  protected readonly outstandingByCurrency: Signal<CreditorOutstandingByCurrency[]> = computed(() => {
    const totals: Map<CurrencyCode, Money> = new Map<CurrencyCode, Money>();
    for(const group of this.detail()?.purchases ?? []) {
      if(group.outstandingMinorUnits <= 0) {
        continue;
      }
      totals.set(group.currencyCode, fromMinorUnits((totals.get(group.currencyCode) ?? 0) + group.outstandingMinorUnits));
    }
    return Array.from(totals.entries()).map(([currencyCode, outstandingMinorUnits]: [CurrencyCode, Money]) => ({
      currencyCode,
      outstandingMinorUnits
    }));
  });
  protected readonly payTargetRemaining: Signal<Money> = computed(() => {
    const target: PayTarget | null = this.payTarget();
    if(target === null) {
      return fromMinorUnits(0);
    }
    if(target.kind === 'expense') {
      return target.group.outstandingMinorUnits;
    }
    if(target.kind === 'full-debt') {
      return this.outstandingByCurrency()[0]?.outstandingMinorUnits ?? fromMinorUnits(0);
    }
    return target.row.remainingMinorUnits;
  });
  protected readonly payTargetGroup: Signal<CreditorPurchaseGroup | null> = computed(() => {
    const target: PayTarget | null = this.payTarget();
    if(target === null || target.kind === 'full-debt') {
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
    if(target.kind === 'full-debt') {
      return 'Pay full debt';
    }
    if(target.kind === 'expense') {
      return target.group.description;
    }
    return `Cuota ${target.row.sequence}/${target.row.installmentCount} · ${this.payTargetGroup()?.description ?? ''}`;
  });
  protected readonly payDialogCurrency: Signal<CurrencyCode> = computed(() => {
    if(this.payTarget()?.kind === 'full-debt') {
      return this.outstandingByCurrency()[0]?.currencyCode ?? 'ARS';
    }
    return this.payTargetGroup()?.currencyCode ?? 'ARS';
  });
  protected readonly payTargetPartyShares: Signal<CreditorInstallmentPartyShare[]> = computed(() => {
    const target: PayTarget | null = this.payTarget();
    if(target === null || target.kind !== 'installment') {
      return [];
    }
    return target.row.partyShares.filter((share: CreditorInstallmentPartyShare) => !share.isPaid);
  });
  protected readonly bankAccounts: Signal<Instrument[]> = computed(() =>
    this.instruments().filter((instrument: Instrument) => instrument.type === 'debit')
  );

  private readonly route: ActivatedRoute = inject(ActivatedRoute);
  private readonly financing: FinancingService = inject(FinancingService);
  private readonly instrumentsService: InstrumentsService = inject(InstrumentsService);
  private readonly destroy$: Subject<void> = new Subject<void>();
  private readonly instruments: WritableSignal<Instrument[]> = signal<Instrument[]>([]);
  private readonly payErrorMessages: Record<string, string> = {
    'Financing.NotACreditorInstallment': 'That installment is on a credit-card plan, not a creditor one.',
    'Financing.InstallmentAlreadyPaid': 'That installment is already marked paid.',
    'Financing.InstallmentAlreadyReversed': 'That installment was reversed and cannot be paid.',
    'Financing.InstallmentNotFound': 'No installment matches that id.',
    'Financing.InvalidPaymentAmount': 'Enter a valid payment amount.',
    'Financing.PaymentExceedsRemaining': "That payment exceeds what's still remaining.",
    'Financing.NoPaymentToUndo': 'There is no payment recorded on this installment to undo.',
    'Financing.InvalidCurrencyCode': 'Choose a currency for this payment.',
    'Financing.PartyShareAlreadyPaid': "That party's share is already paid.",
    'Financing.PartyShareNotDue': 'This cuota is not accrued for party payments yet.',
    'Parties.SettlementExceedsBalance': 'This party has no outstanding balance for this share — they may have already settled it on the Parties page.',
    'Parties.UnknownFundingAccount': 'Choose a debit account registered with the API.'
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

  protected onDialogConfirm(body: CreditorPayDialogConfirm): void {
    const target: PayTarget | null = this.payTarget();
    if(target === null) {
      return;
    }
    if(body.kind === 'party') {
      if(target.kind !== 'installment') {
        return;
      }
      this.runPayPartyShare(target.row.installmentId, { partyId: body.partyId, bankAccountId: body.bankAccountId });
      return;
    }
    if(target.kind === 'full-debt') {
      this.runPayFullDebt({ amountMinorUnits: body.amountMinorUnits, currencyCode: body.currencyCode });
      return;
    }
    const amountBody: PayCreditorInstallment = { amountMinorUnits: body.amountMinorUnits };
    if(target.kind === 'expense') {
      this.runPayExpense(target.group.planId, amountBody);
      return;
    }
    this.runPay(target.row.installmentId, amountBody);
  }

  protected onUndo(installmentId: string): void {
    this.runMutation(this.financing.unpayCreditorInstallment(installmentId));
  }

  protected hasOutstanding(): boolean {
    return (this.detail()?.purchases ?? []).some(
      (group: CreditorPurchaseGroup) => group.outstandingMinorUnits > 0,
    );
  }

  protected onPayFullDebt(): void {
    this.payError.set(null);
    this.lastSettledCount.set(null);
    this.payTarget.set({ kind: 'full-debt' });
  }

  private runPayFullDebt(body: PayCreditorFullDebt): void {
    const creditorId: string | null = this.creditorId;
    if(creditorId === null) {
      return;
    }
    this.payError.set(null);
    this.payStatus.set('busy');
    this.financing
      .payCreditorFullDebt(creditorId, body)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (result: PayCreditorFullDebtResult) => {
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

  private runPayPartyShare(installmentId: string, body: PayCreditorInstallmentPartyShare): void {
    const creditorId: string | null = this.creditorId;
    if(creditorId === null) {
      return;
    }
    this.payError.set(null);
    this.payStatus.set('busy');
    this.financing
      .payCreditorInstallmentPartyShare(installmentId, body)
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

  private loadInstruments(): void {
    this.instrumentsService
      .list()
      .pipe(takeUntil(this.destroy$))
      .subscribe((rows: Instrument[]) => this.instruments.set(rows));
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
    this.loadInstruments();
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
