using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalFinance.Parties.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPartyPurchases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "parties_purchases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PartyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    CategoryName = table.Column<string>(type: "TEXT", nullable: false),
                    Kind = table.Column<string>(type: "TEXT", nullable: false),
                    PurchaseDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    ShareMinorUnits = table.Column<long>(type: "INTEGER", nullable: false),
                    CurrencyCode = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_parties_purchases", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "parties_purchase_installments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PartyPurchaseId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Number = table.Column<int>(type: "INTEGER", nullable: false),
                    DueOn = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    LedgerTransactionId = table.Column<Guid>(type: "TEXT", nullable: true),
                    AmountMinorUnits = table.Column<long>(type: "INTEGER", nullable: false),
                    CurrencyCode = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_parties_purchase_installments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_parties_purchase_installments_parties_purchases_PartyPurchaseId",
                        column: x => x.PartyPurchaseId,
                        principalTable: "parties_purchases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_parties_purchase_installments_LedgerTransactionId",
                table: "parties_purchase_installments",
                column: "LedgerTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_parties_purchase_installments_PartyPurchaseId",
                table: "parties_purchase_installments",
                column: "PartyPurchaseId");

            migrationBuilder.CreateIndex(
                name: "IX_parties_purchases_PartyId",
                table: "parties_purchases",
                column: "PartyId");

            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_party_payable_timeline;");
            migrationBuilder.Sql(ReadViewSqlHelper.Load("vw_party_payable_timeline.sql"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_party_payable_timeline;");
            migrationBuilder.Sql(@"-- Credit-positive movements on the party's payable account: a credit is money borrowed, a debit is a repayment.
CREATE VIEW vw_party_payable_timeline AS
SELECT
    p.Id                       AS PartyId,
    p.Name                     AS PartyName,
    m.AccountId,
    m.TransactionId,
    m.PostedOnUtc              AS MovementOnUtc,
    CASE
        WHEN m.IsReversal = 1          THEN 'Reversal'
        WHEN m.MovementMinorUnits > 0  THEN 'Borrowed from ' || p.Name
        ELSE 'Paid back to ' || p.Name
    END                        AS Description,
    m.MovementMinorUnits       AS DeltaMinorUnits,
    m.RunningBalanceMinorUnits,
    m.CurrencyCode
FROM parties_parties p
INNER JOIN (
    SELECT
        e.AccountId,
        e.TransactionId,
        t.PostedOnUtc,
        CASE WHEN t.OriginalTransactionId IS NOT NULL THEN 1 ELSE 0 END AS IsReversal,
        CASE WHEN e.Direction = 'Credit' THEN e.AmountMinorUnits ELSE -e.AmountMinorUnits END AS MovementMinorUnits,
        SUM(CASE WHEN e.Direction = 'Credit' THEN e.AmountMinorUnits ELSE -e.AmountMinorUnits END)
            OVER (PARTITION BY e.AccountId, e.CurrencyCode
                  ORDER BY t.PostedOnUtc, e.Id
                  ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) AS RunningBalanceMinorUnits,
        e.CurrencyCode
    FROM ledger_entries e
    INNER JOIN ledger_accounts a     ON a.Id = e.AccountId
    INNER JOIN ledger_transactions t ON t.Id = e.TransactionId
    WHERE a.Kind = 'PartyPayable'
) m ON m.AccountId = p.PayableAccountId;");
            migrationBuilder.DropTable(
                name: "parties_purchase_installments");

            migrationBuilder.DropTable(
                name: "parties_purchases");
        }
    }
}
