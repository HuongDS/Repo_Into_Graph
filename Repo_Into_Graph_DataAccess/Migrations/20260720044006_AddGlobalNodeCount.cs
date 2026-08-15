using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Repo_Into_Graph_DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddGlobalNodeCount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "GlobalNodeCount",
                table: "analysis_runs",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GlobalNodeCount",
                table: "analysis_runs");
        }
    }
}
