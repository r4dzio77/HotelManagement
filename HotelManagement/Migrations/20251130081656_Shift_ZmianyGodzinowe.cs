using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HotelManagement.Migrations
{
    /// <inheritdoc />
    public partial class Shift_ZmianyGodzinowe : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EndHour",
                table: "WorkShifts");

            migrationBuilder.DropColumn(
                name: "ShiftType",
                table: "WorkShifts");

            migrationBuilder.DropColumn(
                name: "StartHour",
                table: "WorkShifts");

            migrationBuilder.AddColumn<TimeSpan>(
                name: "EndTime",
                table: "WorkShifts",
                type: "time(6)",
                nullable: false,
                defaultValue: new TimeSpan(0, 0, 0, 0, 0));

            migrationBuilder.AddColumn<TimeSpan>(
                name: "StartTime",
                table: "WorkShifts",
                type: "time(6)",
                nullable: false,
                defaultValue: new TimeSpan(0, 0, 0, 0, 0));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EndTime",
                table: "WorkShifts");

            migrationBuilder.DropColumn(
                name: "StartTime",
                table: "WorkShifts");

            migrationBuilder.AddColumn<TimeSpan>(
                name: "EndHour",
                table: "WorkShifts",
                type: "time(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShiftType",
                table: "WorkShifts",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<TimeSpan>(
                name: "StartHour",
                table: "WorkShifts",
                type: "time(6)",
                nullable: true);
        }
    }
}
