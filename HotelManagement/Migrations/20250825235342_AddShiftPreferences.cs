using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HotelManagement.Migrations
{
    /// <inheritdoc />
    public partial class AddShiftPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "StartTime",
                table: "WorkShifts",
                newName: "ShiftType");

            migrationBuilder.RenameColumn(
                name: "EndTime",
                table: "WorkShifts",
                newName: "Date");

            migrationBuilder.CreateTable(
                name: "ShiftPreferences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    Date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CannotWorkDay = table.Column<bool>(type: "INTEGER", nullable: false),
                    CannotWorkNight = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShiftPreferences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShiftPreferences_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ShiftPreferences_UserId",
                table: "ShiftPreferences",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ShiftPreferences");

            migrationBuilder.RenameColumn(
                name: "ShiftType",
                table: "WorkShifts",
                newName: "StartTime");

            migrationBuilder.RenameColumn(
                name: "Date",
                table: "WorkShifts",
                newName: "EndTime");
        }
    }
}
