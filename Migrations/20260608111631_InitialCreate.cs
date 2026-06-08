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
                name: "FeatureRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AnalysisRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    FeatureName = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeatureRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FeatureRecords_analysis_runs_AnalysisRunId",
                        column: x => x.AnalysisRunId,
                        principalTable: "analysis_runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // 2. Tạo bảng FeatureMethodMappings
            migrationBuilder.CreateTable(
                name: "FeatureMethodMappings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FeatureId = table.Column<Guid>(type: "uuid", nullable: false),
                    MethodSourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    MappedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeatureMethodMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FeatureMethodMappings_FeatureRecords_FeatureId",
                        column: x => x.FeatureId,
                        principalTable: "FeatureRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FeatureMethodMappings_method_sources_MethodSourceId",
                        column: x => x.MethodSourceId,
                        principalTable: "method_sources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // === CHỈ TẠO INDEX CHO CÁC BẢNG MỚI ===
            migrationBuilder.CreateIndex(
                name: "IX_FeatureMethodMappings_FeatureId",
                table: "FeatureMethodMappings",
                column: "FeatureId");

            migrationBuilder.CreateIndex(
                name: "IX_FeatureMethodMappings_MethodSourceId",
                table: "FeatureMethodMappings",
                column: "MethodSourceId");

            migrationBuilder.CreateIndex(
                name: "IX_FeatureRecords_AnalysisRunId",
                table: "FeatureRecords",
                column: "AnalysisRunId");

            // Đã comment Index trùng của bảng method_sources cũ để tránh crash bậy.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FeatureMethodMappings");

            migrationBuilder.DropTable(
                name: "FeatureRecords");
        }
    }
}