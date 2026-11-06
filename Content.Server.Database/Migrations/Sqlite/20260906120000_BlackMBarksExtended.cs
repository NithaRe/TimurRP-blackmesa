using Content.Server.Database;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Content.Server.Database.Migrations.Sqlite
{
    /// <inheritdoc />
    [DbContext(typeof(SqliteServerDbContext))]
    [Migration("20260906120000_BlackMBarksExtended")]
    public partial class BlackMBarksExtended : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "bark_mode",
                table: "profile",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<float>(
                name: "bark_speed",
                table: "profile",
                type: "REAL",
                nullable: false,
                defaultValue: 1f);

            migrationBuilder.AddColumn<float>(
                name: "bark_variation",
                table: "profile",
                type: "REAL",
                nullable: false,
                defaultValue: 0.1f);

            migrationBuilder.Sql("UPDATE profile SET bark_pitch = 1 WHERE bark_pitch <= 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "bark_mode",
                table: "profile");

            migrationBuilder.DropColumn(
                name: "bark_speed",
                table: "profile");

            migrationBuilder.DropColumn(
                name: "bark_variation",
                table: "profile");
        }
    }
}
