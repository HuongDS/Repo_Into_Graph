using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Repo_Into_Graph_DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class Add_NodeType_To_MethodSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "method_sources",
                type: "text",
                nullable: false,
                defaultValue: "Activity");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Type",
                table: "method_sources");
        }
    }
}
