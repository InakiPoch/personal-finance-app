import { CurrencyCode } from '../../../core/types/currency-code';
import { IsoInstant } from '../../../core/types/iso-instant';
import { Money } from '../../../core/types/money';

/** One row of `GET /v1/ledger/transactions`. */
export type TransactionRow = {
  transactionId: string;
  postedOnUtc: IsoInstant;
  description: string;
  amountMinorUnits: Money;
  currencyCode: CurrencyCode;
  isReversal: boolean;
  isReversed: boolean;
  installmentReferenceId: string | null;
  splitReferenceId: string | null;
};
