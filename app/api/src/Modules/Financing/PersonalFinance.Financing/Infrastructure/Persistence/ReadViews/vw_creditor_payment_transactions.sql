-- Ledger transactions referenced by a creditor payment row: they must be undone from the creditor page, not reversed directly.
CREATE VIEW vw_creditor_payment_transactions AS
SELECT DISTINCT LedgerTransactionId AS TransactionId
FROM financing_creditor_installment_payments
WHERE LedgerTransactionId IS NOT NULL;
