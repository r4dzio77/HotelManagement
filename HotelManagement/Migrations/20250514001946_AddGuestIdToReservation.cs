using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HotelManagement.Migrations
{
    /// <inheritdoc />
    public partial class AddGuestIdToReservation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Guests_AspNetUsers_ApplicationUserId",
                table: "Guests");

            migrationBuilder.DropIndex(
                name: "IX_Guests_ApplicationUserId",
                table: "Guests");

            migrationBuilder.DropColumn(
                name: "ApplicationUserId",
                table: "Guests");

            migrationBuilder.AddColumn<int>(
                name: "GuestId",
                table: "AspNetUsers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_GuestId",
                table: "AspNetUsers",
                column: "GuestId");

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_Guests_GuestId",
                table: "AspNetUsers",
                column: "GuestId",
                principalTable: "Guests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_Guests_GuestId",
                table: "AspNetUsers");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_GuestId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "GuestId",
                table: "AspNetUsers");

            migrationBuilder.AddColumn<string>(
                name: "ApplicationUserId",
                table: "Guests",
                type: "varchar(255)",
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Guests_ApplicationUserId",
                table: "Guests",
                column: "ApplicationUserId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Guests_AspNetUsers_ApplicationUserId",
                table: "Guests",
                column: "ApplicationUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
