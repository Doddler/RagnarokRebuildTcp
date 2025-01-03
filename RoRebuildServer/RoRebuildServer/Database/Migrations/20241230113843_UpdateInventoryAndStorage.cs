using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RoRebuildServer.Migrations
{
    /// <inheritdoc />
    public partial class UpdateInventoryAndStorage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ItemDataLength",
                table: "Character",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "NpcFlagsLength",
                table: "Character",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "VersionFormat",
                table: "Character",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "StorageInventory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    StorageData = table.Column<byte[]>(type: "BLOB", nullable: false),
                    UncompressedSize = table.Column<int>(type: "INTEGER", nullable: false),
                    CharacterId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StorageInventory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StorageInventory_Character_CharacterId",
                        column: x => x.CharacterId,
                        principalTable: "Character",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StorageInventory_CharacterId",
                table: "StorageInventory",
                column: "CharacterId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StorageInventory");

            migrationBuilder.DropColumn(
                name: "ItemDataLength",
                table: "Character");

            migrationBuilder.DropColumn(
                name: "NpcFlagsLength",
                table: "Character");

            migrationBuilder.DropColumn(
                name: "VersionFormat",
                table: "Character");
        }
    }
}
