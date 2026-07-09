using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MmuIspApi.Migrations
{
    /// <inheritdoc />
    public partial class AddMissingDomainFieldsAndAuth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PrioritySubjects",
                table: "SystemSettings",
                type: "json",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "UserName",
                table: "Submissions",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "BranchByLevel",
                table: "Students",
                type: "json",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "ChoiceNum",
                table: "Students",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PlacedSelectionId",
                table: "Students",
                type: "varchar(255)",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "PlacedSpecialty",
                table: "Students",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Year",
                table: "Students",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Icon",
                table: "SpecialtyTrees",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "SourceProportional",
                table: "SpecialtyTrees",
                type: "bit(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Year",
                table: "SpecialtyTrees",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "AllowFemale",
                table: "SpecialtyNodes",
                type: "bit(1)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AllowMale",
                table: "SpecialtyNodes",
                type: "bit(1)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GroupTiebreakers",
                table: "SpecialtyNodes",
                type: "json",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Groups",
                table: "SpecialtyNodes",
                type: "json",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "LiseyQuota",
                table: "SpecialtyNodes",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxFemale",
                table: "SpecialtyNodes",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxMale",
                table: "SpecialtyNodes",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MulkiQuota",
                table: "SpecialtyNodes",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QuotaMode",
                table: "SpecialtyNodes",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Tiebreaker",
                table: "SpecialtyNodes",
                type: "json",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "PreAssignLevel",
                table: "Selections",
                type: "int",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Icon",
                table: "Institutions",
                type: "longtext",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(20)",
                oldMaxLength: 20,
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Year",
                table: "Institutions",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Permissions",
                table: "Admins",
                type: "json",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Students_PlacedSelectionId",
                table: "Students",
                column: "PlacedSelectionId");

            migrationBuilder.AddForeignKey(
                name: "FK_Students_Selections_PlacedSelectionId",
                table: "Students",
                column: "PlacedSelectionId",
                principalTable: "Selections",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Students_Selections_PlacedSelectionId",
                table: "Students");

            migrationBuilder.DropIndex(
                name: "IX_Students_PlacedSelectionId",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "PrioritySubjects",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "UserName",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "BranchByLevel",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "ChoiceNum",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "PlacedSelectionId",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "PlacedSpecialty",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "Year",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "Icon",
                table: "SpecialtyTrees");

            migrationBuilder.DropColumn(
                name: "SourceProportional",
                table: "SpecialtyTrees");

            migrationBuilder.DropColumn(
                name: "Year",
                table: "SpecialtyTrees");

            migrationBuilder.DropColumn(
                name: "AllowFemale",
                table: "SpecialtyNodes");

            migrationBuilder.DropColumn(
                name: "AllowMale",
                table: "SpecialtyNodes");

            migrationBuilder.DropColumn(
                name: "GroupTiebreakers",
                table: "SpecialtyNodes");

            migrationBuilder.DropColumn(
                name: "Groups",
                table: "SpecialtyNodes");

            migrationBuilder.DropColumn(
                name: "LiseyQuota",
                table: "SpecialtyNodes");

            migrationBuilder.DropColumn(
                name: "MaxFemale",
                table: "SpecialtyNodes");

            migrationBuilder.DropColumn(
                name: "MaxMale",
                table: "SpecialtyNodes");

            migrationBuilder.DropColumn(
                name: "MulkiQuota",
                table: "SpecialtyNodes");

            migrationBuilder.DropColumn(
                name: "QuotaMode",
                table: "SpecialtyNodes");

            migrationBuilder.DropColumn(
                name: "Tiebreaker",
                table: "SpecialtyNodes");

            migrationBuilder.DropColumn(
                name: "PreAssignLevel",
                table: "Selections");

            migrationBuilder.DropColumn(
                name: "Year",
                table: "Institutions");

            migrationBuilder.DropColumn(
                name: "Permissions",
                table: "Admins");

            migrationBuilder.AlterColumn<string>(
                name: "Icon",
                table: "Institutions",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "longtext",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");
        }
    }
}
