import { CurrencyCode } from '../../../core/types/currency-code';
import { Money } from '../../../core/types/money';

export type MonthlyIncomeRow = {
  month: string;
  amountMinorUnits: Money;
  currencyCode: CurrencyCode;
};
