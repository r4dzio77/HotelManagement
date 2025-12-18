using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HotelManagement.Migrations
{
    public partial class Shift_KilkaGrafikow : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 🔹 1) Dodajemy kolumnę WorkScheduleId do WorkShifts
            migrationBuilder.AddColumn<int>(
                name: "WorkScheduleId",
                table: "WorkShifts",
                type: "int",
                nullable: true);

            // 🔹 2) Tworzymy tabelę WorkSchedules
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

            // 🔹 3) Index na WorkScheduleId w WorkShifts
            migrationBuilder.CreateIndex(
                name: "IX_WorkShifts_WorkScheduleId",
                table: "WorkShifts",
                column: "WorkScheduleId");

            // 🔹 4) Index na CreatedBy w WorkSchedules (opcjonalnie, ale zwykle tak jest)
            migrationBuilder.CreateIndex(
                name: "IX_WorkSchedules_CreatedById",
                table: "WorkSchedules",
                column: "CreatedById");

            // 🔹 5) FK: WorkShifts.WorkScheduleId -> WorkSchedules.Id
            migrationBuilder.AddForeignKey(
                name: "FK_WorkShifts_WorkSchedules_WorkScheduleId",
                table: "WorkShifts",
                column: "WorkScheduleId",
                principalTable: "WorkSchedules",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Cofamy FK
            migrationBuilder.DropForeignKey(
                name: "FK_WorkShifts_WorkSchedules_WorkScheduleId",
                table: "WorkShifts");

            // Cofamy indexy
            migrationBuilder.DropIndex(
                name: "IX_WorkShifts_WorkScheduleId",
                table: "WorkShifts");

            migrationBuilder.DropTable(
                name: "WorkSchedules");

            // Usuwamy kolumnę
            migrationBuilder.DropColumn(
                name: "WorkScheduleId",
                table: "WorkShifts");
        }
    }
}
