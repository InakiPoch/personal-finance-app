using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalFinance.Ledger.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEntryCurrencyCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CurrencyCode",
                table: "ledger_entries",
                type: "TEXT",
                nullable: false,
                defaultValue: "ARS");

            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_ledger_balances;");
            migrationBuilder.Sql(ReadViewSqlHelper.Load("vw_ledger_balances.sql"));
            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_ledger_monthly_expenses;");
            migrationBuilder.Sql(ReadViewSqlHelper.Load("vw_ledger_monthly_expenses.sql"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_ledger_monthly_expenses;");
            migrationBuilder.Sql(@"
CREATE VIEW vw_ledger_monthly_expenses AS
SELECT
    strftime('%Y-%m', t.PostedOnUtc) AS Month,
    a.Name                           AS Category,
    SUM(CASE WHEN e.Direction = 'Debit'
             THEN e.AmountMinorUnits
             ELSE -e.AmountMinorUnits END) AS AmountMinorUnits,
    'ARS'                            AS CurrencyCode
FROM ledger_entries e
INNER JOIN ledger_accounts a     ON a.Id = e.AccountId
INNER JOIN ledger_transactions t ON t.Id = e.TransactionId
WHERE a.Type = 'Expense'
  AND a.Kind NOT IN ('Receivable', 'CardPurchases')
GROUP BY strftime('%Y-%m', t.PostedOnUtc), a.Name;");

            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_ledger_balances;");
            migrationBuilder.Sql(@"
CREATE VIEW vw_ledger_balances AS
SELECT
    a.Id   AS AccountId,
    a.Name AS AccountName,
    a.Type AS AccountType,
    a.Kind AS AccountKind,
    CASE
        WHEN a.Type IN ('Asset', 'Expense') THEN
            COALESCE(SUM(CASE WHEN e.Direction = 'Debit'
                              THEN e.AmountMinorUnits
                              ELSE -e.AmountMinorUnits END), 0)
        ELSE
            -COALESCE(SUM(CASE WHEN e.Direction = 'Debit'
                               THEN e.AmountMinorUnits
                               ELSE -e.AmountMinorUnits END), 0)
    END    AS BalanceMinorUnits,
    'ARS'  AS CurrencyCode
FROM ledger_accounts a
LEFT JOIN ledger_entries e ON e.AccountId = a.Id
GROUP BY a.Id, a.Name, a.Type, a.Kind;");

            migrationBuilder.DropColumn(
                name: "CurrencyCode",
                table: "ledger_entries");
        }
    }
}
