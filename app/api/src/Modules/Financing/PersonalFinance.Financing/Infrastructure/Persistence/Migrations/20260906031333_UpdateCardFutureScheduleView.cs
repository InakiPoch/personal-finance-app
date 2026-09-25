using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalFinance.Financing.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UpdateCardFutureScheduleView : Migration
    {
        // vw_card_future_schedule now joins financing_credit_cards to expose CardName, so the Reporting
        // card_due_by_month query can label its Future rows with a real card name instead of the raw
        // CardId GUID. Keyless ToView mapping is unchanged, so the scaffold is empty — a plain view swap
        // fits in one migration (no table rebuild here).
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
                  AND i.IsReversed = 0
                  AND p.CardId IS NOT NULL;
                """);
        }
    }
}
