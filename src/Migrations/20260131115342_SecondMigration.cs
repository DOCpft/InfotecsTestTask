using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfotecsTestTask.Migrations
{
    /// <inheritdoc />
    public partial class SecondMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Id",
                table: "Values_");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "Id",
                table: "Values_",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }
    }
}
