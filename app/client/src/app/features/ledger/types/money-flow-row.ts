import { CurrencyCode } from '../../../core/types/currency-code';
import { Money } from '../../../core/types/money';

export type MoneyFlowRow = {
  transactionId: string;
  date: string;
  description: string;
  accountName: string;
  kind: 'Income' | 'Outcome';
  amountMinorUnits: Money;
  currencyCode: CurrencyCode;
};
