/** `POST /v1/parties/{id}/borrowings` response — the id of the posted ledger transaction. */
export type BorrowingResult = {
  ledgerTransactionId: string;
};
