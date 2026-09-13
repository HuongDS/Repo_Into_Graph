using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Repo_Into_Graph_DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddLanguageToMethodSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Language",
                table: "method_sources",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Language",
                table: "method_sources");
        }
    }
}
