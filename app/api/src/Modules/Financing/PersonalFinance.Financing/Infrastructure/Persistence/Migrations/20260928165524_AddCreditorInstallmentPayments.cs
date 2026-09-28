using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalFinance.Financing.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCreditorInstallmentPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "financing_creditor_installment_payments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    InstallmentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AmountMinorUnits = table.Column<long>(type: "INTEGER", nullable: false),
                    PaidOnUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    PartyId = table.Column<Guid>(type: "TEXT", nullable: true),
                    SettlementTransactionId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_financing_creditor_installment_payments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_financing_creditor_installment_payments_financing_installments_InstallmentId",
                        column: x => x.InstallmentId,
                        principalTable: "financing_installments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_financing_creditor_installment_payments_InstallmentId",
                table: "financing_creditor_installment_payments",
                column: "InstallmentId");

            // Backfill: one full-amount payment row per already-paid creditor installment, so
            // RemainingMinorUnits/PaidMinorUnits stay consistent with the existing PaidOnUtc stamp.
            // Card installments (plan.CreditorId IS NULL) are untouched. Id format matches how this
            // SQLite database already stores EF-generated Guids (uppercase TEXT).
            migrationBuilder.Sql(
                """
                INSERT INTO financing_creditor_installment_payments (Id, InstallmentId, AmountMinorUnits, PaidOnUtc, PartyId, SettlementTransactionId)
                SELECT
                    upper(
                        substr(hex(randomblob(4)), 1, 8) || '-' ||
                        substr(hex(randomblob(2)), 1, 4) || '-' ||
                        substr(hex(randomblob(2)), 1, 4) || '-' ||
                        substr(hex(randomblob(2)), 1, 4) || '-' ||
                        substr(hex(randomblob(6)), 1, 12)
                    ),
                    i.Id,
                    i.AmountMinorUnits,
                    i.PaidOnUtc,
                    NULL,
                    NULL
                FROM financing_installments i
                JOIN financing_payment_plans p ON p.Id = i.PaymentPlanId
                WHERE i.PaidOnUtc IS NOT NULL AND p.CreditorId IS NOT NULL;
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "financing_creditor_installment_payments");
        }
    }
}
