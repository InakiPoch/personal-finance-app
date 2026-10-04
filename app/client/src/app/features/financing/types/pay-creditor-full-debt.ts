import { CurrencyCode } from '../../../core/types/currency-code';
import { Money } from '../../../core/types/money';

/**
 * Body for `POST /v1/financing/creditor-payables/{creditorId}/pay-full`.
 * `{ null, null }` pays everything remaining, in every currency.
 */
export type PayCreditorFullDebt = {
  amountMinorUnits: Money | null;
  currencyCode: CurrencyCode | null;
};
