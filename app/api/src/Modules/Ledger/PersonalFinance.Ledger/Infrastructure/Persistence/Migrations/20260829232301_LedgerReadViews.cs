using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalFinance.Ledger.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LedgerReadViews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(ReadViewSqlHelper.Load("vw_ledger_balances.sql"));
            migrationBuilder.Sql(ReadViewSqlHelper.Load("vw_ledger_monthly_expenses.sql"));
            migrationBuilder.Sql(ReadViewSqlHelper.Load("vw_card_liability_accrued.sql"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_card_liability_accrued;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_ledger_monthly_expenses;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_ledger_balances;");
        }
    }
}
