import { CurrencyCode } from '../../../core/types/currency-code';
import { Money } from '../../../core/types/money';

export type PartyDebtRow = {
  partyId: string;
  partyName: string;
  netBalanceMinorUnits: Money;
  currencyCode: CurrencyCode;
};
