using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HotelManagement.Migrations
{
    /// <inheritdoc />
    public partial class AddNightAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Guests_LoyaltyCardNumber",
                table: "Guests");

            migrationBuilder.CreateTable(
                name: "BusinessDateStates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    CurrentDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    LastAuditAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    LastAuditUserId = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessDateStates", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BusinessDateStates");

            migrationBuilder.CreateIndex(
                name: "IX_Guests_LoyaltyCardNumber",
                table: "Guests",
                column: "LoyaltyCardNumber",
                unique: true,
                filter: "[LoyaltyCardNumber] IS NOT NULL");
        }
    }
}
