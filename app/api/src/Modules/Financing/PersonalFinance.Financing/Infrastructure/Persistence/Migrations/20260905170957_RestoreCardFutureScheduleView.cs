using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalFinance.Financing.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RestoreCardFutureScheduleView : Migration
    {
        // Recreates vw_card_future_schedule, dropped by AllowCardlessCreditorFinancedPlan so its
        // financing_payment_plans table rebuild could run. The view definition is unchanged.
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_card_future_schedule;");
            migrationBuilder.Sql(ReadViewSqlHelper.Load("vw_card_future_schedule.sql"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_card_future_schedule;");
        }
    }
}
