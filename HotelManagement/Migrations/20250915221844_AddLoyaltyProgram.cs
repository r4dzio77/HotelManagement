using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HotelManagement.Migrations
{
    /// <inheritdoc />
    public partial class AddLoyaltyProgram : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "GuestId",
                table: "LoyaltyPoints",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Reason",
                table: "LoyaltyPoints",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "LoyaltyCardNumber",
                table: "Guests",
                type: "TEXT",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LoyaltyStatus",
                table: "Guests",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TotalNights",
                table: "Guests",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyPoints_GuestId",
                table: "LoyaltyPoints",
                column: "GuestId");

            migrationBuilder.CreateIndex(
                name: "IX_Guests_LoyaltyCardNumber",
                table: "Guests",
                column: "LoyaltyCardNumber",
                unique: true,
                filter: "[LoyaltyCardNumber] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_LoyaltyPoints_Guests_GuestId",
                table: "LoyaltyPoints",
                column: "GuestId",
                principalTable: "Guests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LoyaltyPoints_Guests_GuestId",
                table: "LoyaltyPoints");

            migrationBuilder.DropIndex(
                name: "IX_LoyaltyPoints_GuestId",
                table: "LoyaltyPoints");

            migrationBuilder.DropIndex(
                name: "IX_Guests_LoyaltyCardNumber",
                table: "Guests");

            migrationBuilder.DropColumn(
                name: "GuestId",
                table: "LoyaltyPoints");

            migrationBuilder.DropColumn(
                name: "Reason",
                table: "LoyaltyPoints");

            migrationBuilder.DropColumn(
                name: "LoyaltyCardNumber",
                table: "Guests");

            migrationBuilder.DropColumn(
                name: "LoyaltyStatus",
                table: "Guests");

            migrationBuilder.DropColumn(
                name: "TotalNights",
                table: "Guests");
        }
    }
}
