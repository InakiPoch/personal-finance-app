using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalFinance.Parties.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialPartiesSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "parties_expense_splits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Source = table.Column<string>(type: "TEXT", nullable: false),
                    SourceReferenceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TotalMinorUnits = table.Column<long>(type: "INTEGER", nullable: false),
                    HolderShareMinorUnits = table.Column<long>(type: "INTEGER", nullable: false),
                    AccruedReceivableMinorUnits = table.Column<long>(type: "INTEGER", nullable: false),
                    ReversedReceivableMinorUnits = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_parties_expense_splits", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "parties_inbox_consumed",
                columns: table => new
                {
                    MessageId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Consumer = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ConsumedOnUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_parties_inbox_consumed", x => new { x.MessageId, x.Consumer });
                });

            migrationBuilder.CreateTable(
                name: "parties_outbox_messages",
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
                    table.PrimaryKey("PK_parties_outbox_messages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "parties_parties",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    ReceivableAccountId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_parties_parties", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "parties_expense_split_participants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ExpenseSplitId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PartyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ShareMinorUnits = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_parties_expense_split_participants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_parties_expense_split_participants_parties_expense_splits_ExpenseSplitId",
                        column: x => x.ExpenseSplitId,
                        principalTable: "parties_expense_splits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_parties_expense_split_participants_ExpenseSplitId",
                table: "parties_expense_split_participants",
                column: "ExpenseSplitId");

            migrationBuilder.CreateIndex(
                name: "IX_parties_outbox_messages_MessageId",
                table: "parties_outbox_messages",
                column: "MessageId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_parties_outbox_messages_ProcessedOnUtc",
                table: "parties_outbox_messages",
                column: "ProcessedOnUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "parties_expense_split_participants");

            migrationBuilder.DropTable(
                name: "parties_inbox_consumed");

            migrationBuilder.DropTable(
                name: "parties_outbox_messages");

            migrationBuilder.DropTable(
                name: "parties_parties");

            migrationBuilder.DropTable(
                name: "parties_expense_splits");
        }
    }
}
