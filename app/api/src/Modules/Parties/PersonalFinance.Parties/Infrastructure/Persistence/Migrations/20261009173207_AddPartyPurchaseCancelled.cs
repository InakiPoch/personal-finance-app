using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalFinance.Parties.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPartyPurchaseCancelled : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsCancelled",
                table: "parties_purchases",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsCancelled",
                table: "parties_purchases");
        }
    }
}
