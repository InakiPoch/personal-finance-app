/** `POST /v1/parties/{id}/loans` response — the id of the posted ledger transaction. */
export type LoanResult = {
  ledgerTransactionId: string;
};
