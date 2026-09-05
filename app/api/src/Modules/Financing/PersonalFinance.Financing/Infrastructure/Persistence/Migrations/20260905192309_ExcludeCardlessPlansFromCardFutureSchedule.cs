using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalFinance.Financing.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExcludeCardlessPlansFromCardFutureSchedule : Migration
    {
        // vw_card_future_schedule spanned every un-accrued installment, so once CardId became nullable
        // (AllowCardlessCreditorFinancedPlan) a card-less creditor plan leaked into it with CardId = NULL,
        // and the Reporting card_due_by_month query read that NULL as a non-null Card column and threw.
        // The view SQL now carries "AND p.CardId IS NOT NULL"; recreate it. No table rebuild here — a
        // plain view swap fits in one migration.
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_card_future_schedule;");
            migrationBuilder.Sql(ReadViewSqlHelper.Load("vw_card_future_schedule.sql"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_card_future_schedule;");
            migrationBuilder.Sql(
                """
                CREATE VIEW vw_card_future_schedule AS
                SELECT
                    p.CardId           AS CardId,
                    p.Id               AS PlanId,
                    i.Id               AS InstallmentId,
                    i.Sequence         AS Sequence,
                    i.CycleYear        AS CycleYear,
                    i.CycleMonth       AS CycleMonth,
                    i.AmountMinorUnits AS AmountMinorUnits,
                    'ARS'              AS CurrencyCode
                FROM financing_installments i
                JOIN financing_payment_plans p ON p.Id = i.PaymentPlanId
                WHERE i.AccruedOnUtc IS NULL
                  AND i.IsReversed = 0;
                """);
        }
    }
}
