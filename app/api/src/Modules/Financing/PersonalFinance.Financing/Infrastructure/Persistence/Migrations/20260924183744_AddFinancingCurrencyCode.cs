using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalFinance.Financing.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFinancingCurrencyCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_financing_monthly_statements_CardId_CycleYear_CycleMonth",
                table: "financing_monthly_statements");

            migrationBuilder.AddColumn<string>(
                name: "CurrencyCode",
                table: "financing_payment_plans",
                type: "TEXT",
                nullable: false,
                defaultValue: "ARS");

            migrationBuilder.AddColumn<string>(
                name: "CurrencyCode",
                table: "financing_monthly_statements",
                type: "TEXT",
                nullable: false,
                defaultValue: "ARS");

            migrationBuilder.AddColumn<string>(
                name: "CurrencyCode",
                table: "financing_installments",
                type: "TEXT",
                nullable: false,
                defaultValue: "ARS");

            migrationBuilder.AddColumn<string>(
                name: "CurrencyCode",
                table: "financing_credit_cards",
                type: "TEXT",
                nullable: false,
                defaultValue: "ARS");

            migrationBuilder.CreateIndex(
                name: "IX_financing_monthly_statements_CardId_CycleYear_CycleMonth_CurrencyCode",
                table: "financing_monthly_statements",
                columns: new[] { "CardId", "CycleYear", "CycleMonth", "CurrencyCode" },
                unique: true);

            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_card_future_schedule;");
            migrationBuilder.Sql(ReadViewSqlHelper.Load("vw_card_future_schedule.sql"));
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
");

            migrationBuilder.DropIndex(
                name: "IX_financing_monthly_statements_CardId_CycleYear_CycleMonth_CurrencyCode",
                table: "financing_monthly_statements");

            migrationBuilder.DropColumn(
                name: "CurrencyCode",
                table: "financing_payment_plans");

            migrationBuilder.DropColumn(
                name: "CurrencyCode",
                table: "financing_monthly_statements");

            migrationBuilder.DropColumn(
                name: "CurrencyCode",
                table: "financing_installments");

            migrationBuilder.DropColumn(
                name: "CurrencyCode",
                table: "financing_credit_cards");

            migrationBuilder.CreateIndex(
                name: "IX_financing_monthly_statements_CardId_CycleYear_CycleMonth",
                table: "financing_monthly_statements",
                columns: new[] { "CardId", "CycleYear", "CycleMonth" },
                unique: true);
        }
    }
}
