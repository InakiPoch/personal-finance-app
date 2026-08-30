using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalFinance.Financing.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialFinancingSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "financing_credit_cards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    CutoffDay = table.Column<int>(type: "INTEGER", nullable: false),
                    LiabilityAccountId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ExpenseAccountId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CarriedCreditBalanceMinorUnits = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_financing_credit_cards", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "financing_inbox_consumed",
                columns: table => new
                {
                    MessageId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Consumer = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ConsumedOnUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_financing_inbox_consumed", x => new { x.MessageId, x.Consumer });
                });

            migrationBuilder.CreateTable(
                name: "financing_monthly_statements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CardId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CycleYear = table.Column<int>(type: "INTEGER", nullable: false),
                    CycleMonth = table.Column<int>(type: "INTEGER", nullable: false),
                    AmountDueMinorUnits = table.Column<long>(type: "INTEGER", nullable: false),
                    PaidOnUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_financing_monthly_statements", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "financing_outbox_messages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MessageId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Type = table.Column<string>(type: "TEXT", nullable: false),
                    Payload = table.Column<string>(type: "TEXT", nullable: false),
                    OccurredOnUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ProcessedOnUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    Attempts = table.Column<int>(type: "INTEGER", nullable: false),
                    Error = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_financing_outbox_messages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "financing_payment_plans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CardId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TotalMinorUnits = table.Column<long>(type: "INTEGER", nullable: false),
                    PurchaseDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    InstallmentCount = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_financing_payment_plans", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "financing_installments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PaymentPlanId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Sequence = table.Column<int>(type: "INTEGER", nullable: false),
                    AmountMinorUnits = table.Column<long>(type: "INTEGER", nullable: false),
                    CycleYear = table.Column<int>(type: "INTEGER", nullable: false),
                    CycleMonth = table.Column<int>(type: "INTEGER", nullable: false),
                    AccruedOnUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    StatementId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_financing_installments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_financing_installments_financing_monthly_statements_StatementId",
                        column: x => x.StatementId,
                        principalTable: "financing_monthly_statements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_financing_installments_financing_payment_plans_PaymentPlanId",
                        column: x => x.PaymentPlanId,
                        principalTable: "financing_payment_plans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_financing_installments_PaymentPlanId",
                table: "financing_installments",
                column: "PaymentPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_financing_installments_StatementId",
                table: "financing_installments",
                column: "StatementId");

            migrationBuilder.CreateIndex(
                name: "IX_financing_monthly_statements_CardId_CycleYear_CycleMonth",
                table: "financing_monthly_statements",
                columns: new[] { "CardId", "CycleYear", "CycleMonth" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_financing_outbox_messages_MessageId",
                table: "financing_outbox_messages",
                column: "MessageId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_financing_outbox_messages_ProcessedOnUtc",
                table: "financing_outbox_messages",
                column: "ProcessedOnUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "financing_credit_cards");

            migrationBuilder.DropTable(
                name: "financing_inbox_consumed");

            migrationBuilder.DropTable(
                name: "financing_installments");

            migrationBuilder.DropTable(
                name: "financing_outbox_messages");

            migrationBuilder.DropTable(
                name: "financing_monthly_statements");

            migrationBuilder.DropTable(
                name: "financing_payment_plans");
        }
    }
}
