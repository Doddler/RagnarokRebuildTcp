using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RoRebuildServer.Migrations
{
    /// <inheritdoc />
    public partial class AddSkillLearnedData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "SkillData",
                table: "Character",
                type: "BLOB",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SkillData",
                table: "Character");
        }
    }
}
