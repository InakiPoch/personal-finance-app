-- One row per live (non-reversed, non-reversal) transaction that moves my money through Bank/Cash.
-- Outcome = the full amount credited out of Bank/Cash when the transaction debits an Expense account
-- (card purchases excluded) or a Receivable account (loan / party share fronted). Card accruals and card
-- bill payments never credit Bank/Cash against those legs, so they stay out. Flag/ReceivableAccountIds are structural:
-- a Receivable debit with an Expense leg is 'SharedWith', without one it is a loan ('LentTo').
-- Settlement (Dr Bank/Cash, Cr Receivable, no Income/Expense leg) counts as received, Flag 'PaidBackBy'.
-- Reversals of loans have OriginalTransactionId set and stay out, as do card refunds (no Receivable credit).
-- Borrowing (Dr Bank/Cash, Cr PartyPayable, no Income/Expense leg) counts as received, Flag 'BorrowedFrom'; repayment
-- (Dr PartyPayable, Cr Bank/Cash) is Outcome, Flag 'PaidBackTo'. Party purchases (Dr Expense, Cr PartyPayable) have no
-- Bank/Cash leg, so they stay out. PartyAccountIds holds the Receivable and PartyPayable account ids of the transaction.
-- Category is the Expense account name, 'Lent to parties' for loans or 'Repaid to parties' for repayments.
CREATE VIEW vw_ledger_money_flow AS
SELECT
    t.Id AS TransactionId,
    t.PostedOnUtc AS PostedOnUtc,
    strftime('%Y-%m', t.PostedOnUtc) AS Month,
    COALESCE(t.Description,
             MAX(CASE WHEN a.Type = 'Expense' AND a.Kind NOT IN ('Receivable', 'CardPurchases') THEN a.Name END),
             'Income') AS Description,
    MAX(CASE WHEN a.Type NOT IN ('Expense', 'Income') AND a.Kind NOT IN ('Receivable', 'PartyPayable') THEN a.Name END) AS AccountName,
    SUM(CASE WHEN a.Type = 'Income' AND e.Direction = 'Credit' THEN e.AmountMinorUnits ELSE 0 END)
      + CASE WHEN (SUM(CASE WHEN e.Direction = 'Credit' AND a.Kind = 'Receivable' THEN 1 ELSE 0 END) > 0
          AND SUM(CASE WHEN e.Direction = 'Debit' AND a.Kind = 'Receivable' THEN 1 ELSE 0 END) = 0
          AND SUM(CASE WHEN a.Type IN ('Expense', 'Income') THEN 1 ELSE 0 END) = 0)
             THEN SUM(CASE WHEN a.Kind IN ('Bank', 'Cash') AND e.Direction = 'Debit' THEN e.AmountMinorUnits ELSE 0 END)
             ELSE 0 END
      + CASE WHEN (SUM(CASE WHEN e.Direction = 'Credit' AND a.Kind = 'PartyPayable' THEN 1 ELSE 0 END) > 0
          AND SUM(CASE WHEN e.Direction = 'Debit' AND a.Kind = 'PartyPayable' THEN 1 ELSE 0 END) = 0
          AND SUM(CASE WHEN a.Type IN ('Expense', 'Income') THEN 1 ELSE 0 END) = 0)
             THEN SUM(CASE WHEN a.Kind IN ('Bank', 'Cash') AND e.Direction = 'Debit' THEN e.AmountMinorUnits ELSE 0 END)
             ELSE 0 END AS IncomeMinorUnits,
    CASE WHEN SUM(CASE WHEN e.Direction = 'Debit'
                        AND ((a.Type = 'Expense' AND a.Kind NOT IN ('Receivable', 'CardPurchases')) OR a.Kind IN ('Receivable', 'PartyPayable'))
                       THEN 1 ELSE 0 END) > 0
         THEN SUM(CASE WHEN a.Kind IN ('Bank', 'Cash') AND e.Direction = 'Credit' THEN e.AmountMinorUnits ELSE 0 END)
         ELSE 0 END AS OutcomeMinorUnits,
    e.CurrencyCode AS CurrencyCode,
    COALESCE(MAX(CASE WHEN a.Type = 'Expense' AND a.Kind NOT IN ('Receivable', 'CardPurchases') THEN a.Name END),
             CASE WHEN SUM(CASE WHEN e.Direction = 'Debit' AND a.Kind = 'PartyPayable' THEN 1 ELSE 0 END) > 0
                  THEN 'Repaid to parties' ELSE 'Lent to parties' END) AS Category,
    CASE WHEN (SUM(CASE WHEN e.Direction = 'Credit' AND a.Kind = 'PartyPayable' THEN 1 ELSE 0 END) > 0
          AND SUM(CASE WHEN e.Direction = 'Debit' AND a.Kind = 'PartyPayable' THEN 1 ELSE 0 END) = 0
          AND SUM(CASE WHEN a.Type IN ('Expense', 'Income') THEN 1 ELSE 0 END) = 0) THEN 'BorrowedFrom'
         WHEN SUM(CASE WHEN e.Direction = 'Debit' AND a.Kind = 'PartyPayable' THEN 1 ELSE 0 END) > 0 THEN 'PaidBackTo'
         WHEN (SUM(CASE WHEN e.Direction = 'Credit' AND a.Kind = 'Receivable' THEN 1 ELSE 0 END) > 0
          AND SUM(CASE WHEN e.Direction = 'Debit' AND a.Kind = 'Receivable' THEN 1 ELSE 0 END) = 0
          AND SUM(CASE WHEN a.Type IN ('Expense', 'Income') THEN 1 ELSE 0 END) = 0) THEN 'PaidBackBy'
         WHEN SUM(CASE WHEN e.Direction = 'Debit' AND a.Kind = 'Receivable' THEN 1 ELSE 0 END) = 0 THEN NULL
         WHEN SUM(CASE WHEN e.Direction = 'Debit' AND a.Type = 'Expense' AND a.Kind NOT IN ('Receivable', 'CardPurchases') THEN 1 ELSE 0 END) > 0
         THEN 'SharedWith'
         ELSE 'LentTo' END AS Flag,
    -- Account ids (GUIDs, comma-safe); Reporting resolves party names through the Parties-owned timeline views.
    group_concat(DISTINCT CASE WHEN a.Kind IN ('Receivable', 'PartyPayable') THEN a.Id END) AS PartyAccountIds
FROM ledger_transactions t
INNER JOIN ledger_entries e  ON e.TransactionId = t.Id
INNER JOIN ledger_accounts a ON a.Id = e.AccountId
WHERE t.OriginalTransactionId IS NULL
  AND NOT EXISTS (SELECT 1 FROM ledger_transactions r WHERE r.OriginalTransactionId = t.Id)
GROUP BY t.Id, e.CurrencyCode
HAVING IncomeMinorUnits > 0 OR OutcomeMinorUnits > 0;
