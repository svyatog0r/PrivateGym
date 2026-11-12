using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PrivateGym.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddGuestsToBooking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GuestNames",
                table: "Bookings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuestUserIds",
                table: "Bookings",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GuestNames",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "GuestUserIds",
                table: "Bookings");
        }
    }
}
