using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Repo_Into_Graph_DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class UpdateFeatureMethodMapping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "business_method_mappings");

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
                name: "IX_feature_method_mappings_FeatureId",
                table: "feature_method_mappings",
                column: "FeatureId");

            migrationBuilder.CreateIndex(
                name: "IX_feature_method_mappings_MethodSourceId",
                table: "feature_method_mappings",
                column: "MethodSourceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "feature_method_mappings");

            migrationBuilder.CreateTable(
                name: "business_method_mappings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    MethodSourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    MappedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_business_method_mappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_business_method_mappings_businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "businesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_business_method_mappings_method_sources_MethodSourceId",
                        column: x => x.MethodSourceId,
                        principalTable: "method_sources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_business_method_mappings_BusinessId",
                table: "business_method_mappings",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_business_method_mappings_MethodSourceId",
                table: "business_method_mappings",
                column: "MethodSourceId");
        }
    }
}
