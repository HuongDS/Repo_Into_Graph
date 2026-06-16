using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Repo_Into_Graph.Migrations
{
    /// <inheritdoc />
    public partial class AddRepoMetadataToAnalysisRun : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPublic",
                table: "analysis_runs",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RepoDescription",
                table: "analysis_runs",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RepoLanguage",
                table: "analysis_runs",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RepoName",
                table: "analysis_runs",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RepoOwner",
                table: "analysis_runs",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RepoStars",
                table: "analysis_runs",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RepoUpdatedAt",
                table: "analysis_runs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RepoUrl",
                table: "analysis_runs",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPublic",
                table: "analysis_runs");

            migrationBuilder.DropColumn(
                name: "RepoDescription",
                table: "analysis_runs");

            migrationBuilder.DropColumn(
                name: "RepoLanguage",
                table: "analysis_runs");

            migrationBuilder.DropColumn(
                name: "RepoName",
                table: "analysis_runs");

            migrationBuilder.DropColumn(
                name: "RepoOwner",
                table: "analysis_runs");

            migrationBuilder.DropColumn(
                name: "RepoStars",
                table: "analysis_runs");

            migrationBuilder.DropColumn(
                name: "RepoUpdatedAt",
                table: "analysis_runs");

            migrationBuilder.DropColumn(
                name: "RepoUrl",
                table: "analysis_runs");
        }
    }
}
