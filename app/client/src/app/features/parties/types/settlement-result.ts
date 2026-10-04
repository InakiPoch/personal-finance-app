/** `POST /v1/parties/{id}/settlements` response — the id of the posted ledger transaction. */
export type SettlementResult = {
  ledgerTransactionId: string;
};
