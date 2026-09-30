using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalFinance.Subscriptions.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenameSubscriptionAccountsToSubscription : Migration
    {
        /// <inheritdoc />
        // Deliberate cross-module SQL touch (one-off): Subscriptions knows which ledger accounts are
        // subscription accounts (ExpenseAccountId), so it renames them. Matches by id, never by suffix
        // alone, so a user category like "Office Expense" is untouched. Requires the Ledger schema
        // (ledger_accounts) to be migrated first.
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
UPDATE ledger_accounts
SET Name = substr(Name, 1, length(Name) - length(' Expense')) || ' Subscription'
WHERE Id IN (SELECT ExpenseAccountId FROM subscriptions_templates)
  AND Name LIKE '% Expense';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
UPDATE ledger_accounts
SET Name = substr(Name, 1, length(Name) - length(' Subscription')) || ' Expense'
WHERE Id IN (SELECT ExpenseAccountId FROM subscriptions_templates)
  AND Name LIKE '% Subscription';");
        }
    }
}
