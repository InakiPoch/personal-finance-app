import { CurrencyCode } from '../../../core/types/currency-code';
import { Money } from '../../../core/types/money';

/**
 * Body for `POST /v1/financing/creditor-payables/{creditorId}/pay-full`.
 * `{ null, null }` amount pays everything remaining, in every currency.
 */
export type PayCreditorFullDebt = {
  bankAccountId: string;
  amountMinorUnits: Money | null;
  currencyCode: CurrencyCode | null;
};
