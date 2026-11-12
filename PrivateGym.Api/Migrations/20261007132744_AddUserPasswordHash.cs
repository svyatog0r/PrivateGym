using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace PrivateGym.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddUserPasswordHash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Rooms",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Rooms",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Rooms",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.AddColumn<string>(
                name: "PasswordHash",
                table: "Users",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PasswordHash",
                table: "Users");

            migrationBuilder.InsertData(
                table: "Rooms",
                columns: new[] { "Id", "Capacity", "Description", "Equipment", "IsActive", "MuscleGroups", "Name", "PricePerHour" },
                values: new object[,]
                {
                    { 1, 1, "Тихая комната с беговой дорожкой и эллипсом.", new[] { "Беговая дорожка", "Эллипсоид" }, true, new[] { "Кардио", "Ноги" }, "Кардио-капсула №1", 500m },
                    { 2, 4, "Просторный зал с гантелями и штангами.", new[] { "Гантели", "Штанга", "Скамья" }, true, new[] { "Грудь", "Спина", "Руки" }, "Зал свободных весов", 800m },
                    { 3, 2, "Уютная комната с ковриками и зеркалами.", new[] { "Коврики", "Блоки", "Ремни" }, true, new[] { "Растяжка", "Кор" }, "Йога-рум", 400m }
                });
        }
    }
}
