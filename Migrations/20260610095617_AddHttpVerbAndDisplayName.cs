using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Repo_Into_Graph.Migrations
{
    /// <inheritdoc />
    public partial class AddHttpVerbAndDisplayName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DisplayName",
                table: "method_sources",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HttpVerb",
                table: "method_sources",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CalleeDisplayName",
                table: "call_graph_edges",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CallerDisplayName",
                table: "call_graph_edges",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DisplayName",
                table: "method_sources");

            migrationBuilder.DropColumn(
                name: "HttpVerb",
                table: "method_sources");

            migrationBuilder.DropColumn(
                name: "CalleeDisplayName",
                table: "call_graph_edges");

            migrationBuilder.DropColumn(
                name: "CallerDisplayName",
                table: "call_graph_edges");
        }
    }
}
