using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalFinance.Financing.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LockCreditorSettlementTransactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_creditor_payment_transactions;");
            migrationBuilder.Sql(ReadViewSqlHelper.Load("vw_creditor_payment_transactions.sql"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_creditor_payment_transactions;");
            migrationBuilder.Sql(@"CREATE VIEW vw_creditor_payment_transactions AS
SELECT DISTINCT LedgerTransactionId AS TransactionId
FROM financing_creditor_installment_payments
WHERE LedgerTransactionId IS NOT NULL;");
        }
    }
}
