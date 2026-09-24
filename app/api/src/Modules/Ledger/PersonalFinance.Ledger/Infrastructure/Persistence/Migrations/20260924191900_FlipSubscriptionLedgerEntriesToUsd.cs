using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalFinance.Ledger.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FlipSubscriptionLedgerEntriesToUsd : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
UPDATE ledger_entries
   SET CurrencyCode = 'USD'
 WHERE TransactionId IN (
   SELECT Id FROM ledger_transactions WHERE SubscriptionReferenceId IS NOT NULL
 );
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
UPDATE ledger_entries
   SET CurrencyCode = 'ARS'
 WHERE TransactionId IN (
   SELECT Id FROM ledger_transactions WHERE SubscriptionReferenceId IS NOT NULL
 );
");
        }
    }
}
