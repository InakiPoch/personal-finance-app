using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalFinance.Ledger.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MoneyFlowReceivableAccountIds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_ledger_money_flow;");
            migrationBuilder.Sql(ReadViewSqlHelper.Load("vw_ledger_money_flow.sql"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_ledger_money_flow;");
            migrationBuilder.Sql(@"-- One row per live (non-reversed, non-reversal) transaction that moves my money through Bank/Cash.
-- Outcome = the full amount credited out of Bank/Cash when the transaction debits an Expense account
-- (card purchases excluded) or a Receivable account (loan / party share fronted). Card accruals and card
-- bill payments never credit Bank/Cash against those legs, so they stay out. Flag/PartyName are structural:
-- a Receivable debit with an Expense leg is 'SharedWith', without one it is a loan ('LentTo').
-- Settlement (Dr Bank/Cash, Cr Receivable, no Income/Expense leg) counts as received, Flag 'PaidBackBy'.
-- Reversals of loans have OriginalTransactionId set and stay out, as do card refunds (no Receivable credit).
-- Category is the Expense account name, or 'Lent to parties' for loans. Receivable accounts are named ""{party} Receivable"".
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
    -- ponytail: DISTINCT forces the ',' separator; party names containing commas split badly in the UI.
    group_concat(DISTINCT CASE WHEN a.Kind = 'Receivable'
                               THEN substr(a.Name, 1, length(a.Name) - 11) END) AS PartyName
FROM ledger_transactions t
INNER JOIN ledger_entries e  ON e.TransactionId = t.Id
INNER JOIN ledger_accounts a ON a.Id = e.AccountId
WHERE t.OriginalTransactionId IS NULL
  AND NOT EXISTS (SELECT 1 FROM ledger_transactions r WHERE r.OriginalTransactionId = t.Id)
GROUP BY t.Id, e.CurrencyCode
HAVING IncomeMinorUnits > 0 OR OutcomeMinorUnits > 0;
");
        }
    }
}
