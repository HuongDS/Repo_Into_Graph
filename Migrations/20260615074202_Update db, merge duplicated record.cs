using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Repo_Into_Graph.Migrations
{
    /// <inheritdoc />
    public partial class Updatedbmergeduplicatedrecord : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Language",
                table: "call_graph_edges",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Language",
                table: "call_graph_edges");
        }
    }
}
