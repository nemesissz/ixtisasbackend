using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MmuIspApi.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminCohortScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Cohorts",
                table: "Admins",
                type: "json",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "Cohorts", table: "Admins");
        }
    }
}
