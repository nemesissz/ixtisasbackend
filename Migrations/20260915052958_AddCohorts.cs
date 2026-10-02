using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MmuIspApi.Migrations
{
    /// <inheritdoc />
    public partial class AddCohorts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CohortId",
                table: "Students",
                type: "varchar(255)",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<bool>(
                name: "SourceProportional",
                table: "SpecialtyTrees",
                type: "tinyint(1)",
                nullable: false,
                oldClrType: typeof(ulong),
                oldType: "bit(1)");

            migrationBuilder.AlterColumn<bool>(
                name: "IsArchived",
                table: "SpecialtyTrees",
                type: "tinyint(1)",
                nullable: false,
                oldClrType: typeof(ulong),
                oldType: "bit(1)");

            migrationBuilder.AlterColumn<bool>(
                name: "AllowMale",
                table: "SpecialtyNodes",
                type: "tinyint(1)",
                nullable: true,
                oldClrType: typeof(ulong),
                oldType: "bit(1)",
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "AllowFemale",
                table: "SpecialtyNodes",
                type: "tinyint(1)",
                nullable: true,
                oldClrType: typeof(ulong),
                oldType: "bit(1)",
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "SourceProportional",
                table: "Selections",
                type: "tinyint(1)",
                nullable: false,
                oldClrType: typeof(ulong),
                oldType: "bit(1)");

            migrationBuilder.AlterColumn<bool>(
                name: "ArchivedWithTree",
                table: "Selections",
                type: "tinyint(1)",
                nullable: false,
                oldClrType: typeof(ulong),
                oldType: "bit(1)");

            migrationBuilder.AddColumn<string>(
                name: "CohortIds",
                table: "Selections",
                type: "json",
                nullable: false,
                defaultValue: "[]")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<bool>(
                name: "Field2Required",
                table: "InstitutionLoginConfigs",
                type: "tinyint(1)",
                nullable: false,
                oldClrType: typeof(ulong),
                oldType: "bit(1)");

            migrationBuilder.AlterColumn<bool>(
                name: "Field1Required",
                table: "InstitutionLoginConfigs",
                type: "tinyint(1)",
                nullable: false,
                oldClrType: typeof(ulong),
                oldType: "bit(1)");

            migrationBuilder.CreateTable(
                name: "Cohorts",
                columns: table => new
                {
                    Id = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    InstitutionId = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Label = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Icon = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Year = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsArchived = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    ArchivedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cohorts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Cohorts_Institutions_InstitutionId",
                        column: x => x.InstitutionId,
                        principalTable: "Institutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Students_CohortId_WorkNumber",
                table: "Students",
                columns: new[] { "CohortId", "WorkNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_Cohorts_InstitutionId_SortOrder",
                table: "Cohorts",
                columns: new[] { "InstitutionId", "SortOrder" });

            migrationBuilder.AddForeignKey(
                name: "FK_Students_Cohorts_CohortId",
                table: "Students",
                column: "CohortId",
                principalTable: "Cohorts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Students_Cohorts_CohortId",
                table: "Students");

            migrationBuilder.DropTable(
                name: "Cohorts");

            migrationBuilder.DropIndex(
                name: "IX_Students_CohortId_WorkNumber",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "CohortId",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "CohortIds",
                table: "Selections");

            migrationBuilder.AlterColumn<ulong>(
                name: "SourceProportional",
                table: "SpecialtyTrees",
                type: "bit(1)",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "tinyint(1)");

            migrationBuilder.AlterColumn<ulong>(
                name: "IsArchived",
                table: "SpecialtyTrees",
                type: "bit(1)",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "tinyint(1)");

            migrationBuilder.AlterColumn<ulong>(
                name: "AllowMale",
                table: "SpecialtyNodes",
                type: "bit(1)",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "tinyint(1)",
                oldNullable: true);

            migrationBuilder.AlterColumn<ulong>(
                name: "AllowFemale",
                table: "SpecialtyNodes",
                type: "bit(1)",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "tinyint(1)",
                oldNullable: true);

            migrationBuilder.AlterColumn<ulong>(
                name: "SourceProportional",
                table: "Selections",
                type: "bit(1)",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "tinyint(1)");

            migrationBuilder.AlterColumn<ulong>(
                name: "ArchivedWithTree",
                table: "Selections",
                type: "bit(1)",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "tinyint(1)");

            migrationBuilder.AlterColumn<ulong>(
                name: "Field2Required",
                table: "InstitutionLoginConfigs",
                type: "bit(1)",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "tinyint(1)");

            migrationBuilder.AlterColumn<ulong>(
                name: "Field1Required",
                table: "InstitutionLoginConfigs",
                type: "bit(1)",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "tinyint(1)");
        }
    }
}
