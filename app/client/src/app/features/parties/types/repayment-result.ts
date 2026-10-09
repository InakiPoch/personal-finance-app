/** `POST /v1/parties/{id}/repayments` response — the id of the posted ledger transaction. */
export type RepaymentResult = {
  ledgerTransactionId: string;
};
