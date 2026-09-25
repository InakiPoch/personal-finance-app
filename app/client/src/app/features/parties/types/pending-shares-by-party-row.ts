import { CurrencyCode } from '../../../core/types/currency-code';
import { Money } from '../../../core/types/money';

/** One row of `GET /v1/parties/pending-shares` — a party's not-yet-accrued card-split and creditor-financed installment shares, summed. */
export type PendingSharesByPartyRow = {
  partyId: string;
  scheduledCount: number;
  scheduledTotalMinorUnits: Money;
  currencyCode: CurrencyCode;
};
