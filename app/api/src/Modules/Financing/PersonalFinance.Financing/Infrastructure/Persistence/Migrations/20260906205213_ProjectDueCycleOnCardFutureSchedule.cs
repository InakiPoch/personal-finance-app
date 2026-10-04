using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalFinance.Financing.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProjectDueCycleOnCardFutureSchedule : Migration
    {
        // vw_card_future_schedule now emits the due cycle (statement-close cycle + 1 month, December rolling
        // into the next January) instead of the raw stored cycle, so payment-facing readers show "when the
        // money moves". Column shape is unchanged, so this is a plain view swap in one migration.
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
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
                    CASE WHEN i.CycleMonth = 12 THEN i.CycleYear + 1 ELSE i.CycleYear END AS CycleYear,
                    CASE WHEN i.CycleMonth = 12 THEN 1 ELSE i.CycleMonth + 1 END          AS CycleMonth,
                    i.AmountMinorUnits AS AmountMinorUnits,
                    c.Name             AS CardName,
                    'ARS'              AS CurrencyCode
                FROM financing_installments i
                JOIN financing_payment_plans p ON p.Id = i.PaymentPlanId
                JOIN financing_credit_cards  c ON c.Id = p.CardId
                WHERE i.AccruedOnUtc IS NULL
                  AND i.IsReversed = 0
                  AND p.CardId IS NOT NULL;
                """);
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
                    c.Name             AS CardName,
                    'ARS'              AS CurrencyCode
                FROM financing_installments i
                JOIN financing_payment_plans p ON p.Id = i.PaymentPlanId
                JOIN financing_credit_cards  c ON c.Id = p.CardId
                WHERE i.AccruedOnUtc IS NULL
                  AND i.IsReversed = 0
                  AND p.CardId IS NOT NULL;
                """);
        }
    }
}
