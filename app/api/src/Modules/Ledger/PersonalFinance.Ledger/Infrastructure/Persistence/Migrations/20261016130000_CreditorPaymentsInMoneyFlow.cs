using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalFinance.Ledger.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CreditorPaymentsInMoneyFlow : Migration
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
        }
    }
}
