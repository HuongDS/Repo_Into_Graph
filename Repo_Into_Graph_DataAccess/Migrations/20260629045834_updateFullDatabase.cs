using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Repo_Into_Graph_DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class updateFullDatabase : Migration
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
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    RepoName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    RepoOwner = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    RepoDescription = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    RepoUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RepoLanguage = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RepoStars = table.Column<int>(type: "integer", nullable: true),
                    IsPublic = table.Column<bool>(type: "boolean", nullable: true),
                    RepoUpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_analysis_runs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "few_shot_examples",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Question = table.Column<string>(type: "text", nullable: false),
                    SuggestedAnswer = table.Column<string>(type: "text", nullable: false),
                    Difficulty = table.Column<int>(type: "integer", maxLength: 20, nullable: false),
                    Tag = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_few_shot_examples", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "businesses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AnalysisRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessName = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_businesses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_businesses_analysis_runs_AnalysisRunId",
                        column: x => x.AnalysisRunId,
                        principalTable: "analysis_runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
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
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    Language = table.Column<string>(type: "text", nullable: true)
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
                name: "features",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AnalysisRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    EntryPoint = table.Column<string>(type: "text", nullable: false),
                    MermaidGraph = table.Column<string>(type: "text", nullable: false),
                    DataFlowMermaidGraph = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_features", x => x.Id);
                    table.ForeignKey(
                        name: "FK_features_analysis_runs_AnalysisRunId",
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
                name: "feature_business_mappings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FeatureId = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_feature_business_mappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_feature_business_mappings_businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "businesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_feature_business_mappings_features_FeatureId",
                        column: x => x.FeatureId,
                        principalTable: "features",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "feature_steps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FeatureId = table.Column<Guid>(type: "uuid", nullable: false),
                    StepOrder = table.Column<int>(type: "integer", nullable: false),
                    CallerClass = table.Column<string>(type: "text", nullable: false),
                    CallerMethod = table.Column<string>(type: "text", nullable: false),
                    CalleeClass = table.Column<string>(type: "text", nullable: false),
                    CalleeMethod = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_feature_steps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_feature_steps_features_FeatureId",
                        column: x => x.FeatureId,
                        principalTable: "features",
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
                        name: "FK_feature_method_mappings_features_FeatureId",
                        column: x => x.FeatureId,
                        principalTable: "features",
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
                name: "IX_businesses_AnalysisRunId",
                table: "businesses",
                column: "AnalysisRunId");

            migrationBuilder.CreateIndex(
                name: "IX_call_graph_edges_AnalysisRunId",
                table: "call_graph_edges",
                column: "AnalysisRunId");

            migrationBuilder.CreateIndex(
                name: "IX_feature_business_mappings_BusinessId",
                table: "feature_business_mappings",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_feature_business_mappings_FeatureId",
                table: "feature_business_mappings",
                column: "FeatureId");

            migrationBuilder.CreateIndex(
                name: "IX_feature_method_mappings_FeatureId",
                table: "feature_method_mappings",
                column: "FeatureId");

            migrationBuilder.CreateIndex(
                name: "IX_feature_method_mappings_MethodSourceId",
                table: "feature_method_mappings",
                column: "MethodSourceId");

            migrationBuilder.CreateIndex(
                name: "IX_feature_steps_FeatureId",
                table: "feature_steps",
                column: "FeatureId");

            migrationBuilder.CreateIndex(
                name: "IX_features_AnalysisRunId",
                table: "features",
                column: "AnalysisRunId");

            migrationBuilder.CreateIndex(
                name: "IX_few_shot_examples_Difficulty",
                table: "few_shot_examples",
                column: "Difficulty");

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
                name: "feature_business_mappings");

            migrationBuilder.DropTable(
                name: "feature_method_mappings");

            migrationBuilder.DropTable(
                name: "feature_steps");

            migrationBuilder.DropTable(
                name: "few_shot_examples");

            migrationBuilder.DropTable(
                name: "businesses");

            migrationBuilder.DropTable(
                name: "method_sources");

            migrationBuilder.DropTable(
                name: "features");

            migrationBuilder.DropTable(
                name: "analysis_runs");
        }
    }
}
