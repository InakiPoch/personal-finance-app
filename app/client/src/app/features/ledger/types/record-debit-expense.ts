import { CurrencyCode } from '../../../core/types/currency-code';
import { IsoDate } from '../../../core/types/iso-date';
import { Money } from '../../../core/types/money';
import { DebitExpenseParticipant } from './debit-expense-participant';

export type RecordDebitExpense = {
  amountMinorUnits: Money;
  sourceInstrumentId: string;
  categoryName: string;
  purchaseDate: IsoDate;
  description: string;
  currencyCode: CurrencyCode;
  split?: DebitExpenseParticipant[];
};
