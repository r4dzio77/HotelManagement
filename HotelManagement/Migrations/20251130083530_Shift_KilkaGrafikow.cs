using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HotelManagement.Migrations
{
    /// <inheritdoc />
    public partial class Shift_KilkaGrafikow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // UWAGA: kolumna WorkScheduleId JUŻ istnieje w tabeli WorkShifts,
            // więc NIE dodajemy jej tutaj ponownie.

            migrationBuilder.CreateTable(
                name: "WorkSchedules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Year = table.Column<int>(type: "int", nullable: false),
                    Month = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsPublished = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedById = table.Column<string>(type: "varchar(255)", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkSchedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkSchedules_AspNetUsers_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            // indeks na istniejącej już kolumnie WorkScheduleId
            migrationBuilder.CreateIndex(
                name: "IX_WorkShifts_WorkScheduleId",
                table: "WorkShifts",
                column: "WorkScheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkSchedules_CreatedById",
                table: "WorkSchedules",
                column: "CreatedById");

            // klucz obcy WorkShifts → WorkSchedules
            migrationBuilder.AddForeignKey(
                name: "FK_WorkShifts_WorkSchedules_WorkScheduleId",
                table: "WorkShifts",
                column: "WorkScheduleId",
                principalTable: "WorkSchedules",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // przy wycofywaniu migracji usuwamy FK, tabelę i indeks,
            // ale NIE usuwamy kolumny WorkScheduleId (bo istniała już wcześniej)

            migrationBuilder.DropForeignKey(
                name: "FK_WorkShifts_WorkSchedules_WorkScheduleId",
                table: "WorkShifts");

            migrationBuilder.DropTable(
                name: "WorkSchedules");

            migrationBuilder.DropIndex(
                name: "IX_WorkShifts_WorkScheduleId",
                table: "WorkShifts");

            // brak DropColumn("WorkScheduleId") – kolumna zostaje
        }
    }
}
