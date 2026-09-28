import { CurrencyCode } from '../../../core/types/currency-code';
import { Money } from '../../../core/types/money';

export type CreditorOutstandingByCurrency = {
  currencyCode: CurrencyCode;
  outstandingMinorUnits: Money;
};
