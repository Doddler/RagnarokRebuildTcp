using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RoRebuildServer.Migrations
{
    /// <inheritdoc />
    public partial class SwitchStorageToAccountWide : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StorageInventory_Character_CharacterId",
                table: "StorageInventory");

            migrationBuilder.DropIndex(
                name: "IX_StorageInventory_CharacterId",
                table: "StorageInventory");

            migrationBuilder.DropColumn(
                name: "CharacterId",
                table: "StorageInventory");

            migrationBuilder.AddColumn<int>(
                name: "AccountId",
                table: "StorageInventory",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DataLength",
                table: "Character",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SkillDataLength",
                table: "Character",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_StorageInventory_AccountId",
                table: "StorageInventory",
                column: "AccountId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_StorageInventory_DbUserAccount_AccountId",
                table: "StorageInventory",
                column: "AccountId",
                principalTable: "DbUserAccount",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StorageInventory_DbUserAccount_AccountId",
                table: "StorageInventory");

            migrationBuilder.DropIndex(
                name: "IX_StorageInventory_AccountId",
                table: "StorageInventory");

            migrationBuilder.DropColumn(
                name: "AccountId",
                table: "StorageInventory");

            migrationBuilder.DropColumn(
                name: "DataLength",
                table: "Character");

            migrationBuilder.DropColumn(
                name: "SkillDataLength",
                table: "Character");

            migrationBuilder.AddColumn<Guid>(
                name: "CharacterId",
                table: "StorageInventory",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_StorageInventory_CharacterId",
                table: "StorageInventory",
                column: "CharacterId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_StorageInventory_Character_CharacterId",
                table: "StorageInventory",
                column: "CharacterId",
                principalTable: "Character",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
