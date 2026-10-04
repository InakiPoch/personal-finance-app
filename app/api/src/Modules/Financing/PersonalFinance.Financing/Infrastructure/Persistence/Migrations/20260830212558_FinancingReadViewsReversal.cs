using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalFinance.Financing.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FinancingReadViewsReversal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_card_future_schedule;");
            migrationBuilder.Sql(@"
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
  AND i.IsReversed = 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_card_future_schedule;");
            migrationBuilder.Sql(@"
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
WHERE i.AccruedOnUtc IS NULL;");
        }
    }
}
