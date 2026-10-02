using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MmuIspApi.Migrations
{
    /// <inheritdoc />
    public partial class AddSpecialtyTreeArchiveFlag : Migration
    {
        // QEYD: scaffolder bura bir sıra `tinyint(1)` -> `bit(1)` AlterColumn
        // əməliyyatları da yazmışdı (SourceProportional, AllowMale, AllowFemale,
        // Field1/2Required). Onlar bu dəyişikliyə aid deyil və mövcud bazadakı
        // işlək sütunları dəyişdirərdi — ona görə çıxarılıb. Yalnız iki yeni
        // sütun əlavə olunur; tip qonşu bool sütunlarla eyni (tinyint(1)) saxlanılır.
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "SpecialtyTrees",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedAt",
                table: "SpecialtyTrees",
                type: "datetime(6)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ArchivedAt",
                table: "SpecialtyTrees");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "SpecialtyTrees");
        }
    }
}
