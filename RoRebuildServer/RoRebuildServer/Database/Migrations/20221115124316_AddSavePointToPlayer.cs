using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RoRebuildServer.Migrations
{
    public partial class AddSavePointToPlayer : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SavePoint_Area",
                table: "Character",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SavePoint_MapName",
                table: "Character",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SavePoint_X",
                table: "Character",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SavePoint_Y",
                table: "Character",
                type: "INTEGER",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SavePoint_Area",
                table: "Character");

            migrationBuilder.DropColumn(
                name: "SavePoint_MapName",
                table: "Character");

            migrationBuilder.DropColumn(
                name: "SavePoint_X",
                table: "Character");

            migrationBuilder.DropColumn(
                name: "SavePoint_Y",
                table: "Character");
        }
    }
}
