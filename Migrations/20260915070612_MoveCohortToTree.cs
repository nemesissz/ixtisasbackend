using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MmuIspApi.Migrations
{
    /// <inheritdoc />
    public partial class MoveCohortToTree : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CohortId",
                table: "SpecialtyTrees",
                type: "varchar(255)",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            // Qrup artıq strukturda saxlanılır. Sütun silinməzdən ƏVVƏL hər
            // seçimin qrupunu onun strukturuna köçürürük ki, mövcud məlumat itməsin.
            migrationBuilder.Sql(@"
                UPDATE SpecialtyTrees t
                  JOIN Selections s ON s.TreeId = t.Id
                   SET t.CohortId = JSON_UNQUOTE(JSON_EXTRACT(s.CohortIds, '$[0]'))
                 WHERE t.CohortId IS NULL
                   AND JSON_LENGTH(s.CohortIds) > 0;");

            migrationBuilder.DropColumn(
                name: "CohortIds",
                table: "Selections");


            migrationBuilder.CreateIndex(
                name: "IX_SpecialtyTrees_CohortId",
                table: "SpecialtyTrees",
                column: "CohortId");

            migrationBuilder.AddForeignKey(
                name: "FK_SpecialtyTrees_Cohorts_CohortId",
                table: "SpecialtyTrees",
                column: "CohortId",
                principalTable: "Cohorts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SpecialtyTrees_Cohorts_CohortId",
                table: "SpecialtyTrees");

            migrationBuilder.DropIndex(
                name: "IX_SpecialtyTrees_CohortId",
                table: "SpecialtyTrees");

            migrationBuilder.DropColumn(
                name: "CohortId",
                table: "SpecialtyTrees");

            migrationBuilder.AddColumn<string>(
                name: "CohortIds",
                table: "Selections",
                type: "json",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");
        }
    }
}
