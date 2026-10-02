import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { CardFutureSchedule } from './types/card-future-schedule';
import { CardPurchaseRow } from './types/card-purchase-row';
import { CreatePaymentPlan } from './types/create-payment-plan';
import { CreatePaymentPlanResult } from './types/create-payment-plan-result';
import { CreditorDetail } from './types/creditor-detail';
import { CreditorPayableRow } from './types/creditor-payable-row';
import { DueThisMonthRow } from './types/due-this-month-row';
import { MonthlyStatement } from './types/monthly-statement';
import { MonthlyStatementSummary } from './types/monthly-statement-summary';
import { PayCreditorExpense } from './types/pay-creditor-expense';
import { PayCreditorExpenseResult } from './types/pay-creditor-expense-result';
import { PayCreditorFullDebt } from './types/pay-creditor-full-debt';
import { PayCreditorFullDebtResult } from './types/pay-creditor-full-debt-result';
import { PayCreditorInstallment } from './types/pay-creditor-installment';
import { PayCreditorInstallmentPartyShare } from './types/pay-creditor-installment-party-share';
import { PayCreditorInstallmentPartyShareResult } from './types/pay-creditor-installment-party-share-result';
import { PayCreditorInstallmentResult } from './types/pay-creditor-installment-result';
import { PayInstallment } from './types/pay-installment';
import { PayInstallmentResult } from './types/pay-installment-result';
import { PayStatement } from './types/pay-statement';
import { PayStatementResult } from './types/pay-statement-result';
import { RecentPurchaseRow } from './types/recent-purchase-row';

type RowsEnvelope<T> = { rows: T[] };

@Injectable({ providedIn: 'root' })
export class FinancingService {
  private readonly http: HttpClient = inject(HttpClient);

  createPaymentPlan(body: CreatePaymentPlan): Observable<CreatePaymentPlanResult> {
    return this.http.post<CreatePaymentPlanResult>('financing/payment-plans', body);
  }

  getStatement(id: string): Observable<MonthlyStatement> {
    return this.http.get<MonthlyStatement>(`financing/statements/${id}`);
  }

  payStatement(id: string, body: PayStatement): Observable<PayStatementResult> {
    return this.http.post<PayStatementResult>(`financing/statements/${id}/pay`, body);
  }

  payInstallment(id: string, body: PayInstallment): Observable<PayInstallmentResult> {
    return this.http.post<PayInstallmentResult>(`financing/installments/${id}/pay`, body);
  }

  getFutureSchedule(cardId: string): Observable<CardFutureSchedule> {
    return this.http.get<CardFutureSchedule>(`financing/cards/${cardId}/future-schedule`);
  }

  listStatements(cardId: string): Observable<MonthlyStatementSummary[]> {
    return this.http
      .get<RowsEnvelope<MonthlyStatementSummary>>(`financing/cards/${cardId}/statements`)
    .pipe(map((envelope: RowsEnvelope<MonthlyStatementSummary>) => envelope.rows));
  }

  cardPurchases(cardId: string, month?: string, today?: string): Observable<CardPurchaseRow[]> {
    return this.http
      .get<RowsEnvelope<CardPurchaseRow>>(`financing/cards/${cardId}/purchases`, {
        params: { ...(month ? { month } : {}), ...(today ? { today } : {}) }
      })
    .pipe(map((envelope: RowsEnvelope<CardPurchaseRow>) => envelope.rows));
  }

  recentPurchases(): Observable<RecentPurchaseRow[]> {
    return this.http
      .get<RowsEnvelope<RecentPurchaseRow>>('financing/purchases/recent')
    .pipe(map((envelope: RowsEnvelope<RecentPurchaseRow>) => envelope.rows));
  }

  creditorPayables(): Observable<CreditorPayableRow[]> {
    return this.http
      .get<RowsEnvelope<CreditorPayableRow>>('financing/creditor-payables')
    .pipe(map((envelope: RowsEnvelope<CreditorPayableRow>) => envelope.rows));
  }

  dueThisMonth(month?: string, today?: string): Observable<DueThisMonthRow[]> {
    return this.http
      .get<RowsEnvelope<DueThisMonthRow>>('financing/due-this-month', {
        params: { ...(month ? { month } : {}), ...(today ? { today } : {}) },
      })
    .pipe(map((envelope: RowsEnvelope<DueThisMonthRow>) => envelope.rows));
  }

  creditorDetail(creditorId: string): Observable<CreditorDetail> {
    return this.http.get<CreditorDetail>(`financing/creditor-payables/${creditorId}`);
  }

  payCreditorInstallment(installmentId: string, body: PayCreditorInstallment): Observable<PayCreditorInstallmentResult> {
    return this.http.post<PayCreditorInstallmentResult>(`financing/creditor-installments/${installmentId}/pay`, body);
  }

  unpayCreditorInstallment(installmentId: string): Observable<PayCreditorInstallmentResult> {
    return this.http.post<PayCreditorInstallmentResult>(`financing/creditor-installments/${installmentId}/unpay`, {});
  }

  payCreditorInstallmentPartyShare(installmentId: string, body: PayCreditorInstallmentPartyShare): Observable<PayCreditorInstallmentPartyShareResult> {
    return this.http.post<PayCreditorInstallmentPartyShareResult>(
      `financing/creditor-installments/${installmentId}/pay-party`,
      body
    );
  }

  payCreditorFullDebt(creditorId: string, body: PayCreditorFullDebt): Observable<PayCreditorFullDebtResult> {
    return this.http.post<PayCreditorFullDebtResult>(`financing/creditor-payables/${creditorId}/pay-full`, body);
  }

  payCreditorExpense(planId: string, body: PayCreditorExpense): Observable<PayCreditorExpenseResult> {
    return this.http.post<PayCreditorExpenseResult>(`financing/creditor-purchases/${planId}/pay`, body);
  }
}
