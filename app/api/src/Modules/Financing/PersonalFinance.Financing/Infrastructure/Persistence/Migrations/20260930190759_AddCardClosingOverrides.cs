using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalFinance.Financing.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCardClosingOverrides : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "financing_card_closing_overrides",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CardId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CycleYear = table.Column<int>(type: "INTEGER", nullable: false),
                    CycleMonth = table.Column<int>(type: "INTEGER", nullable: false),
                    ClosingDay = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_financing_card_closing_overrides", x => x.Id);
                    table.ForeignKey(
                        name: "FK_financing_card_closing_overrides_financing_credit_cards_CardId",
                        column: x => x.CardId,
                        principalTable: "financing_credit_cards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_financing_card_closing_overrides_CardId_CycleYear_CycleMonth",
                table: "financing_card_closing_overrides",
                columns: new[] { "CardId", "CycleYear", "CycleMonth" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "financing_card_closing_overrides");
        }
    }
}
