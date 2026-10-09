-- Credit-positive movements on the party's payable account: a credit is money borrowed, a debit is a repayment.
CREATE VIEW vw_party_payable_timeline AS
SELECT
    p.Id                       AS PartyId,
    p.Name                     AS PartyName,
    m.AccountId,
    m.TransactionId,
    m.PostedOnUtc              AS MovementOnUtc,
    CASE
        WHEN m.IsReversal = 1          THEN 'Reversal'
        WHEN m.MovementMinorUnits > 0  THEN 'Borrowed from ' || p.Name
        ELSE 'Paid back to ' || p.Name
    END                        AS Description,
    m.MovementMinorUnits       AS DeltaMinorUnits,
    m.RunningBalanceMinorUnits,
    m.CurrencyCode
FROM parties_parties p
INNER JOIN (
    SELECT
        e.AccountId,
        e.TransactionId,
        t.PostedOnUtc,
        CASE WHEN t.OriginalTransactionId IS NOT NULL THEN 1 ELSE 0 END AS IsReversal,
        CASE WHEN e.Direction = 'Credit' THEN e.AmountMinorUnits ELSE -e.AmountMinorUnits END AS MovementMinorUnits,
        SUM(CASE WHEN e.Direction = 'Credit' THEN e.AmountMinorUnits ELSE -e.AmountMinorUnits END)
            OVER (PARTITION BY e.AccountId, e.CurrencyCode
                  ORDER BY t.PostedOnUtc, e.Id
                  ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) AS RunningBalanceMinorUnits,
        e.CurrencyCode
    FROM ledger_entries e
    INNER JOIN ledger_accounts a     ON a.Id = e.AccountId
    INNER JOIN ledger_transactions t ON t.Id = e.TransactionId
    WHERE a.Kind = 'PartyPayable'
) m ON m.AccountId = p.PayableAccountId;
