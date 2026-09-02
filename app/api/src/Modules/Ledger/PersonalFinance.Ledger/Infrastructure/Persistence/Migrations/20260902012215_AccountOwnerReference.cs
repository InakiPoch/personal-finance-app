using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalFinance.Ledger.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AccountOwnerReference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OwnerReferenceId",
                table: "ledger_accounts",
                type: "TEXT",
                nullable: true);

            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_card_liability_accrued;");
            migrationBuilder.Sql(ReadViewSqlHelper.Load("vw_card_liability_accrued.sql"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_card_liability_accrued;");
            migrationBuilder.Sql(@"
CREATE VIEW vw_card_liability_accrued AS
SELECT
    a.Id   AS CardAccountId,
    a.Name AS CardAccountName,
    -COALESCE(SUM(CASE WHEN e.Direction = 'Debit'
                       THEN e.AmountMinorUnits
                       ELSE -e.AmountMinorUnits END), 0) AS AccruedLiabilityMinorUnits,
    'ARS'  AS CurrencyCode
FROM ledger_accounts a
LEFT JOIN ledger_entries e ON e.AccountId = a.Id
WHERE a.Kind = 'CardLiability'
GROUP BY a.Id, a.Name;");

            migrationBuilder.DropColumn(
                name: "OwnerReferenceId",
                table: "ledger_accounts");
        }
    }
}
