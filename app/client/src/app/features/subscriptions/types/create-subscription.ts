import { CurrencyCode } from '../../../core/types/currency-code';
import { Money } from '../../../core/types/money';
import { Frequency } from './frequency';

export type CreateSubscription = {
  name: string;
  amountMinorUnits: Money;
  category: string;
  fundingAccountId: string;
  frequency: Frequency;
  anchorDay: number;
  currencyCode: CurrencyCode;
};
