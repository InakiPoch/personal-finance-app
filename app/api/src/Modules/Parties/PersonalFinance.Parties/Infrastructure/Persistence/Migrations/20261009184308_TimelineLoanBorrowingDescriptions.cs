using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalFinance.Parties.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TimelineLoanBorrowingDescriptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_current_account_timeline;");
            migrationBuilder.Sql(ReadViewSqlHelper.Load("vw_current_account_timeline.sql"));
            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_party_payable_timeline;");
            migrationBuilder.Sql(ReadViewSqlHelper.Load("vw_party_payable_timeline.sql"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_current_account_timeline;");
            migrationBuilder.Sql(@"-- A debit on the receivable that is funded from Bank/Cash and has no Expense leg is a loan (""Lent to""),
-- detected structurally rather than by description.
CREATE VIEW vw_current_account_timeline AS
SELECT
    p.Id                       AS PartyId,
    p.Name                     AS PartyName,
    m.AccountId,
    m.EntryId,
    m.TransactionId,
    m.PostedOnUtc              AS MovementOnUtc,
    CASE
        WHEN m.IsReversal = 1          THEN 'Reversal'
        WHEN m.MovementMinorUnits > 0
         AND EXISTS (SELECT 1 FROM ledger_entries ce
                     INNER JOIN ledger_accounts ca ON ca.Id = ce.AccountId
                     WHERE ce.TransactionId = m.TransactionId AND ce.Direction = 'Credit' AND ca.Kind IN ('Bank', 'Cash'))
         AND NOT EXISTS (SELECT 1 FROM ledger_entries xe
                         INNER JOIN ledger_accounts xa ON xa.Id = xe.AccountId
                         WHERE xe.TransactionId = m.TransactionId AND xe.Direction = 'Debit'
                           AND xa.Type = 'Expense' AND xa.Kind NOT IN ('Receivable', 'CardPurchases'))
                                       THEN 'Lent to ' || p.Name
        WHEN m.MovementMinorUnits > 0  THEN 'Shared expense'
        ELSE 'Settlement'
    END                        AS Description,
    m.MovementMinorUnits       AS DeltaMinorUnits,
    m.RunningBalanceMinorUnits,
    m.CurrencyCode
FROM parties_parties p
INNER JOIN vw_receivable_account_movements m ON m.AccountId = p.ReceivableAccountId;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_party_payable_timeline;");
            migrationBuilder.Sql(@"-- Credit-positive movements on the party's payable account: a credit is money borrowed or a purchase the party paid, a debit is a repayment.
CREATE VIEW vw_party_payable_timeline AS
SELECT
    p.Id                       AS PartyId,
    p.Name                     AS PartyName,
    m.AccountId,
    m.EntryId,
    m.TransactionId,
    m.PostedOnUtc              AS MovementOnUtc,
    CASE
        WHEN m.IsReversal = 1          THEN 'Reversal'
        WHEN pu.Id IS NOT NULL         THEN 'Paid by ' || p.Name || ': ' || pu.Description
        WHEN m.MovementMinorUnits > 0  THEN 'Borrowed from ' || p.Name
        ELSE 'Paid back to ' || p.Name
    END                        AS Description,
    m.MovementMinorUnits       AS DeltaMinorUnits,
    m.RunningBalanceMinorUnits,
    m.CurrencyCode,
    pu.Id                      AS PurchaseId
FROM parties_parties p
INNER JOIN (
    SELECT
        e.AccountId,
        e.Id AS EntryId,
        e.TransactionId,
        t.PostedOnUtc,
        COALESCE(t.OriginalTransactionId, e.TransactionId) AS SourceTransactionId,
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
) m ON m.AccountId = p.PayableAccountId
LEFT JOIN parties_purchase_installments i ON i.LedgerTransactionId = m.SourceTransactionId
LEFT JOIN parties_purchases pu ON pu.Id = i.PartyPurchaseId;");
        }
    }
}
