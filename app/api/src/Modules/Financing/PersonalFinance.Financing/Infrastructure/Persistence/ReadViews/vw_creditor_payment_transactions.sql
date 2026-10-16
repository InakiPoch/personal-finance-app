-- Ledger transactions referenced by a creditor payment row (the bank outflow and the party-share settlement): they must be undone from the creditor page, not reversed directly.
CREATE VIEW vw_creditor_payment_transactions AS
SELECT LedgerTransactionId AS TransactionId
FROM financing_creditor_installment_payments
WHERE LedgerTransactionId IS NOT NULL
UNION
SELECT SettlementTransactionId
FROM financing_creditor_installment_payments
WHERE SettlementTransactionId IS NOT NULL;
