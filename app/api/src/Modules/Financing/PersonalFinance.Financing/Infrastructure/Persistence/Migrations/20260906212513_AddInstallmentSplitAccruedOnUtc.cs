using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalFinance.Financing.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInstallmentSplitAccruedOnUtc : Migration
    {
        // Two-phase split accrual: the card legs still post when the cycle closes, but a split
        // co-borrower's receivable now posts when the due month arrives (statement-close cycle + 1).
        // SplitAccruedOnUtc marks the second phase as done. Installments that already accrued under
        // the previous one-phase rule posted their receivable and bumped the split metadata at close,
        // so backfill them as split-accrued to keep the due-month pass from double-posting.
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SplitAccruedOnUtc",
                table: "financing_installments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE financing_installments
                SET SplitAccruedOnUtc = AccruedOnUtc
                WHERE AccruedOnUtc IS NOT NULL
                  AND PaymentPlanId IN (
                      SELECT Id FROM financing_payment_plans WHERE SplitReferenceId IS NOT NULL
                  );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SplitAccruedOnUtc",
                table: "financing_installments");
        }
    }
}
