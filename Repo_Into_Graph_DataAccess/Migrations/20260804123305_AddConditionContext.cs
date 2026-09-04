using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Repo_Into_Graph_DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddConditionContext : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ConditionContext",
                table: "feature_steps",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConditionContext",
                table: "call_graph_edges",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConditionContext",
                table: "feature_steps");

            migrationBuilder.DropColumn(
                name: "ConditionContext",
                table: "call_graph_edges");
        }
    }
}
