using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Repo_Into_Graph.Migrations
{
    /// <inheritdoc />
    public partial class AddBusinessFlows : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "business_flows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AnalysisRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    EntryPoint = table.Column<string>(type: "text", nullable: false),
                    MermaidGraph = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_business_flows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_business_flows_analysis_runs_AnalysisRunId",
                        column: x => x.AnalysisRunId,
                        principalTable: "analysis_runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "business_flow_steps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessFlowId = table.Column<Guid>(type: "uuid", nullable: false),
                    CallerClass = table.Column<string>(type: "text", nullable: false),
                    CallerMethod = table.Column<string>(type: "text", nullable: false),
                    CalleeClass = table.Column<string>(type: "text", nullable: false),
                    CalleeMethod = table.Column<string>(type: "text", nullable: false),
                    StepOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_business_flow_steps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_business_flow_steps_business_flows_BusinessFlowId",
                        column: x => x.BusinessFlowId,
                        principalTable: "business_flows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_business_flow_steps_BusinessFlowId",
                table: "business_flow_steps",
                column: "BusinessFlowId");

            migrationBuilder.CreateIndex(
                name: "IX_business_flows_AnalysisRunId",
                table: "business_flows",
                column: "AnalysisRunId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "business_flow_steps");

            migrationBuilder.DropTable(
                name: "business_flows");
        }
    }
}
