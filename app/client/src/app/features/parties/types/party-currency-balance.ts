import { CurrencyCode } from '../../../core/types/currency-code';
import { Money } from '../../../core/types/money';

/** One currency's balance within a `CurrentAccountBalance`. */
export type PartyCurrencyBalance = {
  currencyCode: CurrencyCode;
  balanceMinorUnits: Money;
};
