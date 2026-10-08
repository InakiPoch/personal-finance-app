import { CurrencyCode } from '../../../core/types/currency-code';
import { Money } from '../../../core/types/money';

export type OwedToYouRow = {
  partyId: string;
  partyName: string;
  currencyCode: CurrencyCode;
  amountMinorUnits: Money;
};
