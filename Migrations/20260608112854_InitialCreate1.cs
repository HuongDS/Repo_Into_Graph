using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Repo_Into_Graph.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FeatureMethodMappings_FeatureRecords_FeatureId",
                table: "FeatureMethodMappings");

            migrationBuilder.DropForeignKey(
                name: "FK_FeatureMethodMappings_method_sources_MethodSourceId",
                table: "FeatureMethodMappings");

            migrationBuilder.DropForeignKey(
                name: "FK_FeatureRecords_analysis_runs_AnalysisRunId",
                table: "FeatureRecords");

            migrationBuilder.DropPrimaryKey(
                name: "PK_FeatureRecords",
                table: "FeatureRecords");

            migrationBuilder.DropPrimaryKey(
                name: "PK_FeatureMethodMappings",
                table: "FeatureMethodMappings");

            migrationBuilder.RenameTable(
                name: "FeatureRecords",
                newName: "feature_records");

            migrationBuilder.RenameTable(
                name: "FeatureMethodMappings",
                newName: "feature_method_mappings");

            migrationBuilder.RenameIndex(
                name: "IX_FeatureRecords_AnalysisRunId",
                table: "feature_records",
                newName: "IX_feature_records_AnalysisRunId");

            migrationBuilder.RenameIndex(
                name: "IX_FeatureMethodMappings_MethodSourceId",
                table: "feature_method_mappings",
                newName: "IX_feature_method_mappings_MethodSourceId");

            migrationBuilder.RenameIndex(
                name: "IX_FeatureMethodMappings_FeatureId",
                table: "feature_method_mappings",
                newName: "IX_feature_method_mappings_FeatureId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_feature_records",
                table: "feature_records",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_feature_method_mappings",
                table: "feature_method_mappings",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_feature_method_mappings_feature_records_FeatureId",
                table: "feature_method_mappings",
                column: "FeatureId",
                principalTable: "feature_records",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_feature_method_mappings_method_sources_MethodSourceId",
                table: "feature_method_mappings",
                column: "MethodSourceId",
                principalTable: "method_sources",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_feature_records_analysis_runs_AnalysisRunId",
                table: "feature_records",
                column: "AnalysisRunId",
                principalTable: "analysis_runs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_feature_method_mappings_feature_records_FeatureId",
                table: "feature_method_mappings");

            migrationBuilder.DropForeignKey(
                name: "FK_feature_method_mappings_method_sources_MethodSourceId",
                table: "feature_method_mappings");

            migrationBuilder.DropForeignKey(
                name: "FK_feature_records_analysis_runs_AnalysisRunId",
                table: "feature_records");

            migrationBuilder.DropPrimaryKey(
                name: "PK_feature_records",
                table: "feature_records");

            migrationBuilder.DropPrimaryKey(
                name: "PK_feature_method_mappings",
                table: "feature_method_mappings");

            migrationBuilder.RenameTable(
                name: "feature_records",
                newName: "FeatureRecords");

            migrationBuilder.RenameTable(
                name: "feature_method_mappings",
                newName: "FeatureMethodMappings");

            migrationBuilder.RenameIndex(
                name: "IX_feature_records_AnalysisRunId",
                table: "FeatureRecords",
                newName: "IX_FeatureRecords_AnalysisRunId");

            migrationBuilder.RenameIndex(
                name: "IX_feature_method_mappings_MethodSourceId",
                table: "FeatureMethodMappings",
                newName: "IX_FeatureMethodMappings_MethodSourceId");

            migrationBuilder.RenameIndex(
                name: "IX_feature_method_mappings_FeatureId",
                table: "FeatureMethodMappings",
                newName: "IX_FeatureMethodMappings_FeatureId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_FeatureRecords",
                table: "FeatureRecords",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_FeatureMethodMappings",
                table: "FeatureMethodMappings",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_FeatureMethodMappings_FeatureRecords_FeatureId",
                table: "FeatureMethodMappings",
                column: "FeatureId",
                principalTable: "FeatureRecords",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_FeatureMethodMappings_method_sources_MethodSourceId",
                table: "FeatureMethodMappings",
                column: "MethodSourceId",
                principalTable: "method_sources",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_FeatureRecords_analysis_runs_AnalysisRunId",
                table: "FeatureRecords",
                column: "AnalysisRunId",
                principalTable: "analysis_runs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
