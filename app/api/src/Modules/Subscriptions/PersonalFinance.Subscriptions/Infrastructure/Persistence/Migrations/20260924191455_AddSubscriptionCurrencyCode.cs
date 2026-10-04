using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalFinance.Subscriptions.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscriptionCurrencyCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CurrencyCode",
                table: "subscriptions_templates",
                type: "TEXT",
                nullable: false,
                defaultValue: "ARS");

            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_active_subscriptions;");
            migrationBuilder.Sql(ReadViewSqlHelper.Load("vw_active_subscriptions.sql"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_active_subscriptions;");
            migrationBuilder.Sql(@"
CREATE VIEW vw_active_subscriptions AS
SELECT
    Id              AS SubscriptionId,
    Name            AS Name,
    AmountMinorUnits AS AmountMinorUnits,
    'ARS'           AS CurrencyCode,
    Category        AS Category,
    Frequency       AS Frequency,
    AnchorDay       AS AnchorDay,
    NextDueDate     AS NextDueDate
FROM subscriptions_templates
WHERE IsActive = 1;
");

            migrationBuilder.DropColumn(
                name: "CurrencyCode",
                table: "subscriptions_templates");
        }
    }
}
