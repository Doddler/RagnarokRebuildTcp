using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RoRebuildServer.Migrations
{
    /// <inheritdoc />
    public partial class AddPartyTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OwnedPartyId",
                table: "Character",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PartyId",
                table: "Character",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Party",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PartyName = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Party", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Character_OwnedPartyId",
                table: "Character",
                column: "OwnedPartyId");

            migrationBuilder.CreateIndex(
                name: "IX_Character_PartyId",
                table: "Character",
                column: "PartyId");

            migrationBuilder.AddForeignKey(
                name: "FK_Character_Party_OwnedPartyId",
                table: "Character",
                column: "OwnedPartyId",
                principalTable: "Party",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Character_Party_PartyId",
                table: "Character",
                column: "PartyId",
                principalTable: "Party",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Character_Party_OwnedPartyId",
                table: "Character");

            migrationBuilder.DropForeignKey(
                name: "FK_Character_Party_PartyId",
                table: "Character");

            migrationBuilder.DropTable(
                name: "Party");

            migrationBuilder.DropIndex(
                name: "IX_Character_OwnedPartyId",
                table: "Character");

            migrationBuilder.DropIndex(
                name: "IX_Character_PartyId",
                table: "Character");

            migrationBuilder.DropColumn(
                name: "OwnedPartyId",
                table: "Character");

            migrationBuilder.DropColumn(
                name: "PartyId",
                table: "Character");
        }
    }
}
