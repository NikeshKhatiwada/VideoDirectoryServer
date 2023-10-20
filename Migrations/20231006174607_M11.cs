using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace VideoDirectory_Server.Migrations
{
    /// <inheritdoc />
    public partial class M11 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_Transcript",
                table: "Transcript");

            migrationBuilder.DropIndex(
                name: "IX_Transcript_VideoId",
                table: "Transcript");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "Transcript");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Transcript",
                table: "Transcript",
                column: "VideoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_Transcript",
                table: "Transcript");

            migrationBuilder.AddColumn<int>(
                name: "Id",
                table: "Transcript",
                type: "integer",
                nullable: false,
                defaultValue: 0)
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Transcript",
                table: "Transcript",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_Transcript_VideoId",
                table: "Transcript",
                column: "VideoId",
                unique: true);
        }
    }
}
