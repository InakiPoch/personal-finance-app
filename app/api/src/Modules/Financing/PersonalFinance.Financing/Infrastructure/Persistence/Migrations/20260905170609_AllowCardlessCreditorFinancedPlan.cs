using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalFinance.Financing.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AllowCardlessCreditorFinancedPlan : Migration
    {
        // The CardId AlterColumn triggers a SQLite table rebuild of financing_payment_plans. The
        // vw_card_future_schedule view depends on that table, so it is dropped here; the follow-up
        // migration RestoreCardFutureScheduleView recreates it once the rebuild has settled. (EF defers
        // the rebuild past any trailing SqlOperation, so the recreate cannot live in this migration.)
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_card_future_schedule;");

            migrationBuilder.AlterColumn<Guid>(
                name: "CardId",
                table: "financing_payment_plans",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "TEXT");

            migrationBuilder.AddColumn<Guid>(
                name: "CreditorPayableAccountId",
                table: "financing_payment_plans",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_card_future_schedule;");

            migrationBuilder.DropColumn(
                name: "CreditorPayableAccountId",
                table: "financing_payment_plans");

            migrationBuilder.AlterColumn<Guid>(
                name: "CardId",
                table: "financing_payment_plans",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "TEXT",
                oldNullable: true);
        }
    }
}
