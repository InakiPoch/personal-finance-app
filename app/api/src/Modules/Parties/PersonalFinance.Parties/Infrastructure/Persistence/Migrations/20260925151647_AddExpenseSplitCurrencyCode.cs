using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalFinance.Parties.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddExpenseSplitCurrencyCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CurrencyCode",
                table: "parties_expense_splits",
                type: "TEXT",
                nullable: false,
                defaultValue: "ARS");

            migrationBuilder.AddColumn<string>(
                name: "CurrencyCode",
                table: "parties_expense_split_participants",
                type: "TEXT",
                nullable: false,
                defaultValue: "ARS");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CurrencyCode",
                table: "parties_expense_splits");

            migrationBuilder.DropColumn(
                name: "CurrencyCode",
                table: "parties_expense_split_participants");
        }
    }
}
