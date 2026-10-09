using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalFinance.Ledger.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PartyDebtsInMoneyFlow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_ledger_monthly_expenses;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_ledger_monthly_incomes;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_ledger_money_flow;");
            migrationBuilder.Sql(ReadViewSqlHelper.Load("vw_ledger_money_flow.sql"));
            migrationBuilder.Sql(ReadViewSqlHelper.Load("vw_ledger_monthly_expenses.sql"));
            migrationBuilder.Sql(ReadViewSqlHelper.Load("vw_ledger_monthly_incomes.sql"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_ledger_monthly_expenses;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_ledger_monthly_incomes;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_ledger_money_flow;");
            migrationBuilder.Sql(@"-- One row per live (non-reversed, non-reversal) transaction that moves my money through Bank/Cash.
-- Outcome = the full amount credited out of Bank/Cash when the transaction debits an Expense account
-- (card purchases excluded) or a Receivable account (loan / party share fronted). Card accruals and card
-- bill payments never credit Bank/Cash against those legs, so they stay out. Flag/ReceivableAccountIds are structural:
-- a Receivable debit with an Expense leg is 'SharedWith', without one it is a loan ('LentTo').
-- Settlement (Dr Bank/Cash, Cr Receivable, no Income/Expense leg) counts as received, Flag 'PaidBackBy'.
-- Reversals of loans have OriginalTransactionId set and stay out, as do card refunds (no Receivable credit).
-- Category is the Expense account name, or 'Lent to parties' for loans.
CREATE VIEW vw_ledger_money_flow AS
SELECT
    t.Id AS TransactionId,
    t.PostedOnUtc AS PostedOnUtc,
    strftime('%Y-%m', t.PostedOnUtc) AS Month,
    COALESCE(t.Description,
             MAX(CASE WHEN a.Type = 'Expense' AND a.Kind NOT IN ('Receivable', 'CardPurchases') THEN a.Name END),
             'Income') AS Description,
    MAX(CASE WHEN a.Type NOT IN ('Expense', 'Income') AND a.Kind <> 'Receivable' THEN a.Name END) AS AccountName,
    SUM(CASE WHEN a.Type = 'Income' AND e.Direction = 'Credit' THEN e.AmountMinorUnits ELSE 0 END)
      + CASE WHEN (SUM(CASE WHEN e.Direction = 'Credit' AND a.Kind = 'Receivable' THEN 1 ELSE 0 END) > 0
          AND SUM(CASE WHEN e.Direction = 'Debit' AND a.Kind = 'Receivable' THEN 1 ELSE 0 END) = 0
          AND SUM(CASE WHEN a.Type IN ('Expense', 'Income') THEN 1 ELSE 0 END) = 0)
             THEN SUM(CASE WHEN a.Kind IN ('Bank', 'Cash') AND e.Direction = 'Debit' THEN e.AmountMinorUnits ELSE 0 END)
             ELSE 0 END AS IncomeMinorUnits,
    CASE WHEN SUM(CASE WHEN e.Direction = 'Debit'
                        AND ((a.Type = 'Expense' AND a.Kind NOT IN ('Receivable', 'CardPurchases')) OR a.Kind = 'Receivable')
                       THEN 1 ELSE 0 END) > 0
         THEN SUM(CASE WHEN a.Kind IN ('Bank', 'Cash') AND e.Direction = 'Credit' THEN e.AmountMinorUnits ELSE 0 END)
         ELSE 0 END AS OutcomeMinorUnits,
    e.CurrencyCode AS CurrencyCode,
    COALESCE(MAX(CASE WHEN a.Type = 'Expense' AND a.Kind NOT IN ('Receivable', 'CardPurchases') THEN a.Name END),
             'Lent to parties') AS Category,
    CASE WHEN (SUM(CASE WHEN e.Direction = 'Credit' AND a.Kind = 'Receivable' THEN 1 ELSE 0 END) > 0
          AND SUM(CASE WHEN e.Direction = 'Debit' AND a.Kind = 'Receivable' THEN 1 ELSE 0 END) = 0
          AND SUM(CASE WHEN a.Type IN ('Expense', 'Income') THEN 1 ELSE 0 END) = 0) THEN 'PaidBackBy'
         WHEN SUM(CASE WHEN e.Direction = 'Debit' AND a.Kind = 'Receivable' THEN 1 ELSE 0 END) = 0 THEN NULL
         WHEN SUM(CASE WHEN e.Direction = 'Debit' AND a.Type = 'Expense' AND a.Kind NOT IN ('Receivable', 'CardPurchases') THEN 1 ELSE 0 END) > 0
         THEN 'SharedWith'
         ELSE 'LentTo' END AS Flag,
    -- Account ids (GUIDs, comma-safe); Reporting resolves party names through the Parties-owned timeline view.
    group_concat(DISTINCT CASE WHEN a.Kind = 'Receivable' THEN a.Id END) AS ReceivableAccountIds
FROM ledger_transactions t
INNER JOIN ledger_entries e  ON e.TransactionId = t.Id
INNER JOIN ledger_accounts a ON a.Id = e.AccountId
WHERE t.OriginalTransactionId IS NULL
  AND NOT EXISTS (SELECT 1 FROM ledger_transactions r WHERE r.OriginalTransactionId = t.Id)
GROUP BY t.Id, e.CurrencyCode
HAVING IncomeMinorUnits > 0 OR OutcomeMinorUnits > 0;
");
            migrationBuilder.Sql(@"-- ""Spent from bank & cash"": everything that left Bank/Cash, summed per category. Derived from the money-flow view
-- so the dashboard and Recent Money Movements always reconcile.
CREATE VIEW vw_ledger_monthly_expenses AS
SELECT
    Month,
    Category,
    SUM(OutcomeMinorUnits) AS AmountMinorUnits,
    CurrencyCode
FROM vw_ledger_money_flow
WHERE OutcomeMinorUnits > 0
GROUP BY Month, Category, CurrencyCode;
");
            migrationBuilder.Sql(@"-- Money received = Income credits plus settlements (Dr Bank/Cash, Cr Receivable, no Income/Expense leg).
-- Settlements are counted only while live (not reversed, not a reversal), so a loan's mirror entry never counts.
CREATE VIEW vw_ledger_monthly_incomes AS
SELECT Month, SUM(AmountMinorUnits) AS AmountMinorUnits, CurrencyCode
FROM (
    SELECT
        strftime('%Y-%m', t.PostedOnUtc) AS Month,
        CASE WHEN e.Direction = 'Credit' THEN e.AmountMinorUnits ELSE -e.AmountMinorUnits END AS AmountMinorUnits,
        e.CurrencyCode AS CurrencyCode
    FROM ledger_entries e
    INNER JOIN ledger_accounts a     ON a.Id = e.AccountId
    INNER JOIN ledger_transactions t ON t.Id = e.TransactionId
    WHERE a.Type = 'Income' AND a.Kind = 'Income'
    UNION ALL
    SELECT strftime('%Y-%m', t.PostedOnUtc), e.AmountMinorUnits, e.CurrencyCode
    FROM ledger_entries e
    INNER JOIN ledger_accounts a     ON a.Id = e.AccountId
    INNER JOIN ledger_transactions t ON t.Id = e.TransactionId
    WHERE e.Direction = 'Debit' AND a.Kind IN ('Bank', 'Cash')
      AND t.OriginalTransactionId IS NULL
      AND NOT EXISTS (SELECT 1 FROM ledger_transactions r WHERE r.OriginalTransactionId = t.Id)
      AND EXISTS (SELECT 1 FROM ledger_entries c INNER JOIN ledger_accounts ca ON ca.Id = c.AccountId
                  WHERE c.TransactionId = t.Id AND c.Direction = 'Credit' AND ca.Kind = 'Receivable')
      AND NOT EXISTS (SELECT 1 FROM ledger_entries x INNER JOIN ledger_accounts xa ON xa.Id = x.AccountId
                      WHERE x.TransactionId = t.Id AND (xa.Type IN ('Expense', 'Income') OR (x.Direction = 'Debit' AND xa.Kind = 'Receivable')))
)
GROUP BY Month, CurrencyCode;
");
        }
    }
}
