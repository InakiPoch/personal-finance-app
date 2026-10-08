using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalFinance.Parties.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TimelineLoanLabel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_current_account_timeline;");
            migrationBuilder.Sql(ReadViewSqlHelper.Load("vw_current_account_timeline.sql"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_current_account_timeline;");
            migrationBuilder.Sql(@"CREATE VIEW vw_current_account_timeline AS
SELECT
    p.Id                       AS PartyId,
    p.Name                     AS PartyName,
    m.AccountId,
    m.TransactionId,
    m.PostedOnUtc              AS MovementOnUtc,
    CASE
        WHEN m.IsReversal = 1          THEN 'Reversal'
        WHEN m.MovementMinorUnits > 0  THEN 'Shared expense'
        ELSE 'Settlement'
    END                        AS Description,
    m.MovementMinorUnits       AS DeltaMinorUnits,
    m.RunningBalanceMinorUnits,
    m.CurrencyCode
FROM parties_parties p
INNER JOIN vw_receivable_account_movements m ON m.AccountId = p.ReceivableAccountId;");
        }
    }
}
