import { CurrencyCode } from '../../../core/types/currency-code';
import { Money } from '../../../core/types/money';

export type AccountBalance = {
  accountId: string;
  balanceMinorUnits: Money;
  currencyCode: CurrencyCode;
  formatted: string;
};
