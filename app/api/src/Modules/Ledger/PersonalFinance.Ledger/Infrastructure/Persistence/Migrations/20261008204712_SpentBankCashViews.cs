using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalFinance.Ledger.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SpentBankCashViews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_ledger_monthly_expenses;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_ledger_money_flow;");
            migrationBuilder.Sql(ReadViewSqlHelper.Load("vw_ledger_money_flow.sql"));
            migrationBuilder.Sql(ReadViewSqlHelper.Load("vw_ledger_monthly_expenses.sql"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_ledger_monthly_expenses;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_ledger_money_flow;");
            migrationBuilder.Sql(@"CREATE VIEW vw_ledger_money_flow AS
SELECT
    t.Id AS TransactionId,
    t.PostedOnUtc AS PostedOnUtc,
    strftime('%Y-%m', t.PostedOnUtc) AS Month,
    COALESCE(t.Description,
             MAX(CASE WHEN a.Type = 'Expense' AND a.Kind NOT IN ('Receivable', 'CardPurchases') THEN a.Name END),
             'Income') AS Description,
    MAX(CASE WHEN a.Type NOT IN ('Expense', 'Income') AND a.Kind <> 'Receivable' THEN a.Name END) AS AccountName,
    SUM(CASE WHEN a.Type = 'Income' AND e.Direction = 'Credit' THEN e.AmountMinorUnits ELSE 0 END) AS IncomeMinorUnits,
    SUM(CASE WHEN a.Type = 'Expense' AND a.Kind NOT IN ('Receivable', 'CardPurchases') AND e.Direction = 'Debit'
             THEN e.AmountMinorUnits ELSE 0 END) AS OutcomeMinorUnits,
    e.CurrencyCode AS CurrencyCode
FROM ledger_transactions t
INNER JOIN ledger_entries e  ON e.TransactionId = t.Id
INNER JOIN ledger_accounts a ON a.Id = e.AccountId
WHERE t.OriginalTransactionId IS NULL
  AND NOT EXISTS (SELECT 1 FROM ledger_transactions r WHERE r.OriginalTransactionId = t.Id)
GROUP BY t.Id, e.CurrencyCode
HAVING IncomeMinorUnits > 0 OR OutcomeMinorUnits > 0;");
            migrationBuilder.Sql(@"CREATE VIEW vw_ledger_monthly_expenses AS
SELECT
    strftime('%Y-%m', t.PostedOnUtc) AS Month,
    a.Name AS Category,
    SUM(CASE WHEN e.Direction = 'Debit'
          THEN e.AmountMinorUnits
          ELSE -e.AmountMinorUnits END
    ) AS AmountMinorUnits,
    e.CurrencyCode AS CurrencyCode
FROM ledger_entries e
INNER JOIN ledger_accounts a     ON a.Id = e.AccountId
INNER JOIN ledger_transactions t ON t.Id = e.TransactionId
WHERE a.Type = 'Expense'
  AND a.Kind NOT IN ('Receivable', 'CardPurchases')
GROUP BY strftime('%Y-%m', t.PostedOnUtc), a.Name, e.CurrencyCode;");
        }
    }
}
