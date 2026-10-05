using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MmuIspApi.Migrations
{
    /// <inheritdoc />
    public partial class AddMonitorSessionSelection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SelectionId",
                table: "MonitorSessions",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "SelectionName",
                table: "MonitorSessions",
                type: "varchar(300)",
                maxLength: 300,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_MonitorSessions_SelectionId",
                table: "MonitorSessions",
                column: "SelectionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MonitorSessions_SelectionId",
                table: "MonitorSessions");

            migrationBuilder.DropColumn(
                name: "SelectionId",
                table: "MonitorSessions");

            migrationBuilder.DropColumn(
                name: "SelectionName",
                table: "MonitorSessions");
        }
    }
}
