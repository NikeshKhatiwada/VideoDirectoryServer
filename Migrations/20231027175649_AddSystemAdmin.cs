using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoDirectory_Server.Migrations
{
    /// <inheritdoc />
    public partial class AddSystemAdmin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "SystemAdmins",
                columns: new[] { "Id", "CreatedAt", "FirstName", "LastName", "LastUpdatedAt", "Password", "Username" },
                values: new object[] { new Guid("ba3d69ee-3e96-4899-9ba3-908cbe0312f4"), new DateTime(2023, 10, 27, 17, 56, 49, 119, DateTimeKind.Utc).AddTicks(394), "Nikesh", "Khatiwada", new DateTime(2023, 10, 27, 17, 56, 49, 119, DateTimeKind.Utc).AddTicks(399), "$2a$11$lZMgpdQHizZy0EDxREU7kuGegApWKXe1FsAI8EGKQ4GvYrB60hUvG", "NK" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "SystemAdmins",
                keyColumn: "Id",
                keyValue: new Guid("ba3d69ee-3e96-4899-9ba3-908cbe0312f4"));
        }
    }
}
