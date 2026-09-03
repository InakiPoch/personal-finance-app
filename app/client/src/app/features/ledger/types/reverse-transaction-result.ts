/**
 * `POST /v1/ledger/transactions/{id}/reversal` response. The reversal is append-only:
 * `reversalTransactionId` is a new transaction, the original is untouched.
 * `compensatingEntryPosted` is `true` when the reversal also posted a card credit for an
 * already-paid installment.
 */
export type ReverseTransactionResult = {
  reversalTransactionId: string;
  originalTransactionId: string;
  compensatingEntryPosted: boolean;
};
