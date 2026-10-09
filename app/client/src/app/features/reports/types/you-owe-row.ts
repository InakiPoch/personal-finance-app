import { CurrencyCode } from '../../../core/types/currency-code';
import { Money } from '../../../core/types/money';

export type YouOweRow = {
  partyId: string;
  partyName: string;
  currencyCode: CurrencyCode;
  amountMinorUnits: Money;
};
