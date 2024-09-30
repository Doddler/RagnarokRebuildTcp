using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RoRebuildServer.Migrations
{
    /// <inheritdoc />
    public partial class AddCharacterSummaryData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "CharacterSummary",
                table: "Character",
                type: "BLOB",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CharacterSummary",
                table: "Character");
        }
    }
}
