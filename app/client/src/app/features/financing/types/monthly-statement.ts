import { CurrencyCode } from '../../../core/types/currency-code';
import { IsoInstant } from '../../../core/types/iso-instant';
import { Money } from '../../../core/types/money';
import { MonthlyStatementInstallment } from './monthly-statement-installment';

/**
 * `GET /v1/financing/statements/{id}` response. `installments` is a DTO field on the
 * object, not a `{ rows }` envelope — the service returns it as-is.
 */
export type MonthlyStatement = {
  statementId: string;
  cardId: string;
  cardName: string;
  cycleYear: number;
  cycleMonth: number;
  amountDueMinorUnits: Money;
  isPaid: boolean;
  paidOnUtc: IsoInstant | null;
  installments: MonthlyStatementInstallment[];
  currencyCode: CurrencyCode;
};
