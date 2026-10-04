using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalFinance.Financing.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PaymentPlanSplitLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SplitReferenceId",
                table: "financing_payment_plans",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "financing_payment_plan_split_participants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PaymentPlanId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PartyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ReceivableAccountId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Weight = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_financing_payment_plan_split_participants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_financing_payment_plan_split_participants_financing_payment_plans_PaymentPlanId",
                        column: x => x.PaymentPlanId,
                        principalTable: "financing_payment_plans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_financing_payment_plan_split_participants_PaymentPlanId",
                table: "financing_payment_plan_split_participants",
                column: "PaymentPlanId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "financing_payment_plan_split_participants");

            migrationBuilder.DropColumn(
                name: "SplitReferenceId",
                table: "financing_payment_plans");
        }
    }
}
