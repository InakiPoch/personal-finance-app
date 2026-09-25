using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalFinance.Subscriptions.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FlipSubscriptionsToUsd : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE subscriptions_templates SET CurrencyCode = 'USD';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE subscriptions_templates SET CurrencyCode = 'ARS';");
        }
    }
}
