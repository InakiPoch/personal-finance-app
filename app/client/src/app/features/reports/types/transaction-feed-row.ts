import { CurrencyCode } from '../../../core/types/currency-code';
import { IsoInstant } from '../../../core/types/iso-instant';
import { Money } from '../../../core/types/money';
import { TransactionKind } from './transaction-kind';

/** One row of `GET /v1/reports/transactions` (and `…/{id}`), newest first. */
export type TransactionFeedRow = {
  id: string;
  postedOnUtc: IsoInstant;
  kind: TransactionKind;
  description: string;
  fromAccounts: string[];
  toAccounts: string[];
  amountMinorUnits: Money;
  currencyCode: CurrencyCode;
  isUndoEntry: boolean;
  isUndone: boolean;
  impactLines: string[];
};
