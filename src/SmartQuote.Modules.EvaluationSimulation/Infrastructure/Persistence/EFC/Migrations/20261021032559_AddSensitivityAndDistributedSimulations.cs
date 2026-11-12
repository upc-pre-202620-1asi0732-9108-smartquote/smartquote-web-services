using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartQuote.Modules.EvaluationSimulation.Infrastructure.Persistence.EFC.Migrations
{
    /// <inheritdoc />
    public partial class AddSensitivityAndDistributedSimulations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "distribution",
                schema: "evaluation_simulation",
                table: "simulation_runs",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "mode",
                schema: "evaluation_simulation",
                table: "simulation_runs",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "SingleSupplier");

            migrationBuilder.AddColumn<DateOnly>(
                name: "valid_until",
                schema: "evaluation_simulation",
                table: "simulation_quotation_snapshots",
                type: "date",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "sensitivity_analyses",
                schema: "evaluation_simulation",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    scenario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_id = table.Column<string>(type: "text", nullable: false),
                    criteria_version = table.Column<int>(type: "integer", nullable: false),
                    input_fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    inputs = table.Column<string>(type: "jsonb", nullable: false),
                    exchange_rate = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sensitivity_analyses", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sensitivity_configurations",
                schema: "evaluation_simulation",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    weights = table.Column<string>(type: "jsonb", nullable: false),
                    results = table.Column<string>(type: "jsonb", nullable: false),
                    tied_winner_ids = table.Column<string>(type: "jsonb", nullable: false),
                    recommended_quotation_id = table.Column<string>(type: "text", nullable: true),
                    analysis_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sensitivity_configurations", x => x.id);
                    table.ForeignKey(
                        name: "fk_sensitivity_configurations_sensitivity_analyses_analysis_id",
                        column: x => x.analysis_id,
                        principalSchema: "evaluation_simulation",
                        principalTable: "sensitivity_analyses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_sensitivity_analyses_request_id_created_at",
                schema: "evaluation_simulation",
                table: "sensitivity_analyses",
                columns: new[] { "request_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_sensitivity_configurations_analysis_id",
                schema: "evaluation_simulation",
                table: "sensitivity_configurations",
                column: "analysis_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sensitivity_configurations",
                schema: "evaluation_simulation");

            migrationBuilder.DropTable(
                name: "sensitivity_analyses",
                schema: "evaluation_simulation");

            migrationBuilder.DropColumn(
                name: "distribution",
                schema: "evaluation_simulation",
                table: "simulation_runs");

            migrationBuilder.DropColumn(
                name: "mode",
                schema: "evaluation_simulation",
                table: "simulation_runs");

            migrationBuilder.DropColumn(
                name: "valid_until",
                schema: "evaluation_simulation",
                table: "simulation_quotation_snapshots");
        }
    }
}
