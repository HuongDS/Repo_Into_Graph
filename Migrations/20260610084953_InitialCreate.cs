using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Repo_Into_Graph.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "analysis_runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RepositoryPath = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_analysis_runs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "call_graph_edges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AnalysisRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    CallerClass = table.Column<string>(type: "text", nullable: false),
                    CallerMethod = table.Column<string>(type: "text", nullable: false),
                    CalleeClass = table.Column<string>(type: "text", nullable: false),
                    CalleeMethod = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_call_graph_edges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_call_graph_edges_analysis_runs_AnalysisRunId",
                        column: x => x.AnalysisRunId,
                        principalTable: "analysis_runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "feature_records",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AnalysisRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    FeatureName = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_feature_records", x => x.Id);
                    table.ForeignKey(
                        name: "FK_feature_records_analysis_runs_AnalysisRunId",
                        column: x => x.AnalysisRunId,
                        principalTable: "analysis_runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "method_sources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AnalysisRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClassName = table.Column<string>(type: "text", nullable: false),
                    MethodName = table.Column<string>(type: "text", nullable: false),
                    SourceCode = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_method_sources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_method_sources_analysis_runs_AnalysisRunId",
                        column: x => x.AnalysisRunId,
                        principalTable: "analysis_runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "feature_method_mappings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FeatureId = table.Column<Guid>(type: "uuid", nullable: false),
                    MethodSourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    MappedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_feature_method_mappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_feature_method_mappings_feature_records_FeatureId",
                        column: x => x.FeatureId,
                        principalTable: "feature_records",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_feature_method_mappings_method_sources_MethodSourceId",
                        column: x => x.MethodSourceId,
                        principalTable: "method_sources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_call_graph_edges_AnalysisRunId",
                table: "call_graph_edges",
                column: "AnalysisRunId");

            migrationBuilder.CreateIndex(
                name: "IX_feature_method_mappings_FeatureId",
                table: "feature_method_mappings",
                column: "FeatureId");

            migrationBuilder.CreateIndex(
                name: "IX_feature_method_mappings_MethodSourceId",
                table: "feature_method_mappings",
                column: "MethodSourceId");

            migrationBuilder.CreateIndex(
                name: "IX_feature_records_AnalysisRunId",
                table: "feature_records",
                column: "AnalysisRunId");

            migrationBuilder.CreateIndex(
                name: "IX_method_sources_AnalysisRunId",
                table: "method_sources",
                column: "AnalysisRunId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "call_graph_edges");

            migrationBuilder.DropTable(
                name: "feature_method_mappings");

            migrationBuilder.DropTable(
                name: "feature_records");

            migrationBuilder.DropTable(
                name: "method_sources");

            migrationBuilder.DropTable(
                name: "analysis_runs");
        }
    }
}
