using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MmuIspApi.Migrations
{
    /// <inheritdoc />
    public partial class FixSpecialtyNodeParentCascade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SpecialtyNodes_SpecialtyNodes_ParentId",
                table: "SpecialtyNodes");

            migrationBuilder.AddForeignKey(
                name: "FK_SpecialtyNodes_SpecialtyNodes_ParentId",
                table: "SpecialtyNodes",
                column: "ParentId",
                principalTable: "SpecialtyNodes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SpecialtyNodes_SpecialtyNodes_ParentId",
                table: "SpecialtyNodes");

            migrationBuilder.AddForeignKey(
                name: "FK_SpecialtyNodes_SpecialtyNodes_ParentId",
                table: "SpecialtyNodes",
                column: "ParentId",
                principalTable: "SpecialtyNodes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
