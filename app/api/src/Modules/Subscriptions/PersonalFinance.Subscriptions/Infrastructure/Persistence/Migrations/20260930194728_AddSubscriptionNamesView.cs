using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalFinance.Subscriptions.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscriptionNamesView : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(ReadViewSqlHelper.Load("vw_subscription_names.sql"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_subscription_names;");
        }
    }
}
