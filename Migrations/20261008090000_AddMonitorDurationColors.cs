using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MmuIspApi.Migrations
{
    /// <inheritdoc />
    public partial class AddMonitorDurationColors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "WarnMin",
                table: "MonitorConfigs",
                type: "int",
                nullable: false,
                defaultValue: 10);

            migrationBuilder.AddColumn<int>(
                name: "DangerMin",
                table: "MonitorConfigs",
                type: "int",
                nullable: false,
                defaultValue: 20);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "WarnMin", table: "MonitorConfigs");
            migrationBuilder.DropColumn(name: "DangerMin", table: "MonitorConfigs");
        }
    }
}
