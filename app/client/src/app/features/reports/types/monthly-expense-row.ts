import { CurrencyCode } from '../../../core/types/currency-code';
import { Money } from '../../../core/types/money';

export type MonthlyExpenseRow = {
  month: string;
  category: string;
  amountMinorUnits: Money;
  currencyCode: CurrencyCode;
};
