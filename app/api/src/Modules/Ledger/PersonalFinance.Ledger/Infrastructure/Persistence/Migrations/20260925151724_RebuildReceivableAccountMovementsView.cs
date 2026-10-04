using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalFinance.Ledger.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RebuildReceivableAccountMovementsView : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_receivable_account_movements;");
            migrationBuilder.Sql(ReadViewSqlHelper.Load("vw_receivable_account_movements.sql"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_receivable_account_movements;");
            migrationBuilder.Sql(@"
CREATE VIEW vw_receivable_account_movements AS
SELECT
    e.AccountId,
    a.Name                AS AccountName,
    e.TransactionId,
    t.PostedOnUtc,
    CASE WHEN t.OriginalTransactionId IS NOT NULL THEN 1 ELSE 0 END AS IsReversal,
    CASE WHEN e.Direction = 'Debit'
         THEN e.AmountMinorUnits
         ELSE -e.AmountMinorUnits END AS MovementMinorUnits,
    SUM(CASE WHEN e.Direction = 'Debit'
             THEN e.AmountMinorUnits
             ELSE -e.AmountMinorUnits END)
        OVER (PARTITION BY e.AccountId
              ORDER BY t.PostedOnUtc, e.Id
              ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) AS RunningBalanceMinorUnits,
    'ARS'                 AS CurrencyCode
FROM ledger_entries e
INNER JOIN ledger_accounts a     ON a.Id = e.AccountId
INNER JOIN ledger_transactions t ON t.Id = e.TransactionId
WHERE a.Kind = 'Receivable';
");
        }
    }
}
