using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalFinance.Ledger.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialLedgerSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ledger_accounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Type = table.Column<string>(type: "TEXT", nullable: false),
                    Kind = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ledger_accounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ledger_inbox_consumed",
                columns: table => new
                {
                    MessageId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Consumer = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ConsumedOnUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ledger_inbox_consumed", x => new { x.MessageId, x.Consumer });
                });

            migrationBuilder.CreateTable(
                name: "ledger_outbox_messages",
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
                    table.PrimaryKey("PK_ledger_outbox_messages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ledger_transactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PostedOnUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    OriginalTransactionId = table.Column<Guid>(type: "TEXT", nullable: true),
                    SplitReferenceId = table.Column<Guid>(type: "TEXT", nullable: true),
                    InstallmentReferenceId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ledger_transactions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ledger_entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    AccountId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Direction = table.Column<string>(type: "TEXT", nullable: false),
                    AmountMinorUnits = table.Column<long>(type: "INTEGER", nullable: false),
                    TransactionId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ledger_entries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ledger_entries_ledger_accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "ledger_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ledger_entries_ledger_transactions_TransactionId",
                        column: x => x.TransactionId,
                        principalTable: "ledger_transactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ledger_entries_AccountId",
                table: "ledger_entries",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_ledger_entries_TransactionId",
                table: "ledger_entries",
                column: "TransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_ledger_outbox_messages_MessageId",
                table: "ledger_outbox_messages",
                column: "MessageId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ledger_outbox_messages_ProcessedOnUtc",
                table: "ledger_outbox_messages",
                column: "ProcessedOnUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ledger_transactions_OriginalTransactionId",
                table: "ledger_transactions",
                column: "OriginalTransactionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ledger_entries");

            migrationBuilder.DropTable(
                name: "ledger_inbox_consumed");

            migrationBuilder.DropTable(
                name: "ledger_outbox_messages");

            migrationBuilder.DropTable(
                name: "ledger_accounts");

            migrationBuilder.DropTable(
                name: "ledger_transactions");
        }
    }
}
