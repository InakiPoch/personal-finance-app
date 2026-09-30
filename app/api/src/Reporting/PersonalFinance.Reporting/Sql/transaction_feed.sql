-- One row per ledger leg (entry), newest transaction first; the handler groups the legs per transaction.
-- Optional filters: $transactionId, $accountId (transaction touches the account), $from (inclusive yyyy-MM-dd),
-- $to (exclusive yyyy-MM-dd, the handler passes the day after the requested inclusive end).
SELECT
    l.TransactionId,
    l.PostedOnUtc,
    l.Description,
    l.OriginalTransactionId,
    l.IsReversed,
    l.InstallmentReferenceId,
    l.SplitReferenceId,
    l.SubscriptionReferenceId,
    l.AccountName,
    l.AccountKind,
    l.Direction,
    l.AmountMinorUnits,
    l.CurrencyCode,
    il.Sequence,
    il.InstallmentCount,
    il.PlanDescription,
    il.CardName,
    il.CreditorName,
    il.IsPaid,
    il.IsReversed AS InstallmentIsReversed,
    sn.Name AS SubscriptionName
FROM vw_ledger_transaction_legs l
LEFT JOIN vw_installment_labels il ON il.InstallmentId = l.InstallmentReferenceId
LEFT JOIN vw_subscription_names sn ON sn.TemplateId = l.SubscriptionReferenceId
WHERE ($transactionId IS NULL OR l.TransactionId = $transactionId)
  AND ($accountId IS NULL OR l.TransactionId IN (SELECT TransactionId FROM vw_ledger_transaction_legs WHERE AccountId = $accountId))
  AND ($from IS NULL OR l.PostedOnUtc >= $from)
  AND ($to IS NULL OR l.PostedOnUtc < $to)
ORDER BY l.PostedOnUtc DESC, l.TransactionId, l.EntryId;
