using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalFinance.Parties.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPartyPayableAccount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PayableAccountId",
                table: "parties_parties",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // Backfill: one PartyPayable liability account per existing party; OwnerReferenceId carries the party id only while linking.
            migrationBuilder.Sql(@"INSERT INTO ledger_accounts (Id, Name, Type, Kind, OwnerReferenceId)
SELECT upper(hex(randomblob(4)) || '-' || hex(randomblob(2)) || '-4' || substr(hex(randomblob(2)), 2) || '-A' || substr(hex(randomblob(2)), 2) || '-' || hex(randomblob(6))),
       Name || ' Payable', 'Liability', 'PartyPayable', Id
FROM parties_parties;");
            migrationBuilder.Sql(@"UPDATE parties_parties
SET PayableAccountId = (SELECT a.Id FROM ledger_accounts a WHERE a.Kind = 'PartyPayable' AND a.OwnerReferenceId = parties_parties.Id);");
            migrationBuilder.Sql("UPDATE ledger_accounts SET OwnerReferenceId = NULL WHERE Kind = 'PartyPayable';");
            migrationBuilder.Sql(ReadViewSqlHelper.Load("vw_party_payable_timeline.sql"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_party_payable_timeline;");
            migrationBuilder.DropColumn(
                name: "PayableAccountId",
                table: "parties_parties");
        }
    }
}
