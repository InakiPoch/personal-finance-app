import { CurrencyCode } from '../../../core/types/currency-code';
import { Money } from '../../../core/types/money';

export type DueThisMonthRow = {
  kind: 'card' | 'creditor';
  sourceId: string;
  sourceName: string;
  currencyCode: CurrencyCode;
  amountMinorUnits: Money;
};
