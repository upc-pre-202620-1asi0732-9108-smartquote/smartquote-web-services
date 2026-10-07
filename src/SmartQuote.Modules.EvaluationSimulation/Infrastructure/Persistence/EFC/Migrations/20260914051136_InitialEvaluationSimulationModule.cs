using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartQuote.Modules.EvaluationSimulation.Infrastructure.Persistence.EFC.Migrations
{
    /// <inheritdoc />
    public partial class InitialEvaluationSimulationModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "evaluation_simulation");

            migrationBuilder.CreateTable(
                name: "evaluation_scenarios",
                schema: "evaluation_simulation",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    supersedes_scenario_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_evaluation_scenarios", x => x.id);
                    table.ForeignKey(
                        name: "fk_evaluation_scenarios_evaluation_scenarios_supersedes_scenar~",
                        column: x => x.supersedes_scenario_id,
                        principalSchema: "evaluation_simulation",
                        principalTable: "evaluation_scenarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "evaluation_criteria",
                schema: "evaluation_simulation",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    target_field = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    category = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    mode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    comparison_operator = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    expected_value = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    unit_of_measure = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    weight = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false),
                    evaluation_scenario_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_evaluation_criteria", x => x.id);
                    table.ForeignKey(
                        name: "fk_evaluation_criteria_evaluation_scenarios_evaluation_scenari~",
                        column: x => x.evaluation_scenario_id,
                        principalSchema: "evaluation_simulation",
                        principalTable: "evaluation_scenarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "simulation_runs",
                schema: "evaluation_simulation",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    evaluation_scenario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    criteria_version = table.Column<int>(type: "integer", nullable: false),
                    input_fingerprint = table.Column<string>(type: "char(64)", nullable: false),
                    executed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_simulation_runs", x => x.id);
                    table.ForeignKey(
                        name: "fk_simulation_runs_evaluation_scenarios_evaluation_scenario_id",
                        column: x => x.evaluation_scenario_id,
                        principalSchema: "evaluation_simulation",
                        principalTable: "evaluation_scenarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "quotation_evaluations",
                schema: "evaluation_simulation",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    quotation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_eligible = table.Column<bool>(type: "boolean", nullable: false),
                    total_score = table.Column<decimal>(type: "numeric(7,4)", nullable: false),
                    ranking_position = table.Column<int>(type: "integer", nullable: true),
                    simulation_run_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_quotation_evaluations", x => x.id);
                    table.ForeignKey(
                        name: "fk_quotation_evaluations_simulation_runs_simulation_run_id",
                        column: x => x.simulation_run_id,
                        principalSchema: "evaluation_simulation",
                        principalTable: "simulation_runs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "simulation_quotation_snapshots",
                schema: "evaluation_simulation",
                columns: table => new
                {
                    source_quotation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    simulation_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_quotation_version = table.Column<long>(type: "bigint", nullable: false),
                    supplier_reference_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    supplier_business_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    supplier_tax_identifier = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    currency = table.Column<string>(type: "char(3)", nullable: false),
                    delivery_lead_time_days = table.Column<int>(type: "integer", nullable: false),
                    verified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    captured_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_simulation_quotation_snapshots", x => new { x.simulation_run_id, x.source_quotation_id });
                    table.ForeignKey(
                        name: "fk_simulation_quotation_snapshots_simulation_runs_simulation_r~",
                        column: x => x.simulation_run_id,
                        principalSchema: "evaluation_simulation",
                        principalTable: "simulation_runs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "simulation_recommendations",
                schema: "evaluation_simulation",
                columns: table => new
                {
                    simulation_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quotation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    score = table.Column<decimal>(type: "numeric(7,4)", nullable: false),
                    explanation = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_simulation_recommendations", x => x.simulation_run_id);
                    table.ForeignKey(
                        name: "fk_simulation_recommendations_simulation_runs_simulation_run_id",
                        column: x => x.simulation_run_id,
                        principalSchema: "evaluation_simulation",
                        principalTable: "simulation_runs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "simulation_request_snapshots",
                schema: "evaluation_simulation",
                columns: table => new
                {
                    simulation_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_request_version = table.Column<long>(type: "bigint", nullable: false),
                    required_date = table.Column<DateOnly>(type: "date", nullable: false),
                    priority = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    captured_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_simulation_request_snapshots", x => x.simulation_run_id);
                    table.ForeignKey(
                        name: "fk_simulation_request_snapshots_simulation_runs_simulation_run~",
                        column: x => x.simulation_run_id,
                        principalSchema: "evaluation_simulation",
                        principalTable: "simulation_runs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "criterion_results",
                schema: "evaluation_simulation",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    evaluation_criterion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    passed = table.Column<bool>(type: "boolean", nullable: false),
                    normalized_score = table.Column<decimal>(type: "numeric(7,4)", nullable: false),
                    weighted_contribution = table.Column<decimal>(type: "numeric(7,4)", nullable: false),
                    explanation = table.Column<string>(type: "text", nullable: false),
                    quotation_evaluation_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_criterion_results", x => x.id);
                    table.ForeignKey(
                        name: "fk_criterion_results_quotation_evaluations_quotation_evaluatio~",
                        column: x => x.quotation_evaluation_id,
                        principalSchema: "evaluation_simulation",
                        principalTable: "quotation_evaluations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "exclusion_reasons",
                schema: "evaluation_simulation",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    evaluation_criterion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    explanation = table.Column<string>(type: "text", nullable: false),
                    quotation_evaluation_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_exclusion_reasons", x => x.id);
                    table.ForeignKey(
                        name: "fk_exclusion_reasons_quotation_evaluations_quotation_evaluatio~",
                        column: x => x.quotation_evaluation_id,
                        principalSchema: "evaluation_simulation",
                        principalTable: "quotation_evaluations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "simulation_quotation_lines",
                schema: "evaluation_simulation",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_quotation_line_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source_requested_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    line_number = table.Column<int>(type: "integer", nullable: false),
                    description = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    unit_of_measure = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    simulation_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_quotation_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_simulation_quotation_lines", x => x.id);
                    table.ForeignKey(
                        name: "fk_simulation_quotation_lines_simulation_quotation_snapshots_s~",
                        columns: x => new { x.simulation_run_id, x.source_quotation_id },
                        principalSchema: "evaluation_simulation",
                        principalTable: "simulation_quotation_snapshots",
                        principalColumns: new[] { "simulation_run_id", "source_quotation_id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "simulation_request_items",
                schema: "evaluation_simulation",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_requested_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    line_number = table.Column<int>(type: "integer", nullable: false),
                    description = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    unit_of_measure = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    simulation_request_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_simulation_request_items", x => x.id);
                    table.ForeignKey(
                        name: "fk_simulation_request_items_simulation_request_snapshots_simul~",
                        column: x => x.simulation_request_id,
                        principalSchema: "evaluation_simulation",
                        principalTable: "simulation_request_snapshots",
                        principalColumn: "simulation_run_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "simulation_quotation_specifications",
                schema: "evaluation_simulation",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    value = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    unit_of_measure = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    simulation_quotation_line_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_simulation_quotation_specifications", x => x.id);
                    table.ForeignKey(
                        name: "fk_simulation_quotation_specifications_simulation_quotation_li~",
                        column: x => x.simulation_quotation_line_id,
                        principalSchema: "evaluation_simulation",
                        principalTable: "simulation_quotation_lines",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "simulation_request_requirements",
                schema: "evaluation_simulation",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_requirement_id = table.Column<Guid>(type: "uuid", nullable: true),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    comparison_operator = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    expected_value = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    unit_of_measure = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    is_mandatory = table.Column<bool>(type: "boolean", nullable: false),
                    simulation_request_item_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_simulation_request_requirements", x => x.id);
                    table.ForeignKey(
                        name: "fk_simulation_request_requirements_simulation_request_items_si~",
                        column: x => x.simulation_request_item_id,
                        principalSchema: "evaluation_simulation",
                        principalTable: "simulation_request_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_criterion_results_quotation_evaluation_id",
                schema: "evaluation_simulation",
                table: "criterion_results",
                column: "quotation_evaluation_id");

            migrationBuilder.CreateIndex(
                name: "ix_evaluation_criteria_evaluation_scenario_id",
                schema: "evaluation_simulation",
                table: "evaluation_criteria",
                column: "evaluation_scenario_id");

            migrationBuilder.CreateIndex(
                name: "ix_evaluation_scenarios_purchase_request_id_version",
                schema: "evaluation_simulation",
                table: "evaluation_scenarios",
                columns: new[] { "purchase_request_id", "version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_evaluation_scenarios_supersedes_scenario_id",
                schema: "evaluation_simulation",
                table: "evaluation_scenarios",
                column: "supersedes_scenario_id");

            migrationBuilder.CreateIndex(
                name: "ix_exclusion_reasons_quotation_evaluation_id",
                schema: "evaluation_simulation",
                table: "exclusion_reasons",
                column: "quotation_evaluation_id");

            migrationBuilder.CreateIndex(
                name: "ix_quotation_evaluations_simulation_run_id",
                schema: "evaluation_simulation",
                table: "quotation_evaluations",
                column: "simulation_run_id");

            migrationBuilder.CreateIndex(
                name: "ix_simulation_quotation_lines_simulation_run_id_source_quotati~",
                schema: "evaluation_simulation",
                table: "simulation_quotation_lines",
                columns: new[] { "simulation_run_id", "source_quotation_id" });

            migrationBuilder.CreateIndex(
                name: "ix_simulation_quotation_specifications_simulation_quotation_li~",
                schema: "evaluation_simulation",
                table: "simulation_quotation_specifications",
                column: "simulation_quotation_line_id");

            migrationBuilder.CreateIndex(
                name: "ix_simulation_request_items_simulation_request_id",
                schema: "evaluation_simulation",
                table: "simulation_request_items",
                column: "simulation_request_id");

            migrationBuilder.CreateIndex(
                name: "ix_simulation_request_requirements_simulation_request_item_id",
                schema: "evaluation_simulation",
                table: "simulation_request_requirements",
                column: "simulation_request_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_simulation_runs_evaluation_scenario_id",
                schema: "evaluation_simulation",
                table: "simulation_runs",
                column: "evaluation_scenario_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "criterion_results",
                schema: "evaluation_simulation");

            migrationBuilder.DropTable(
                name: "evaluation_criteria",
                schema: "evaluation_simulation");

            migrationBuilder.DropTable(
                name: "exclusion_reasons",
                schema: "evaluation_simulation");

            migrationBuilder.DropTable(
                name: "simulation_quotation_specifications",
                schema: "evaluation_simulation");

            migrationBuilder.DropTable(
                name: "simulation_recommendations",
                schema: "evaluation_simulation");

            migrationBuilder.DropTable(
                name: "simulation_request_requirements",
                schema: "evaluation_simulation");

            migrationBuilder.DropTable(
                name: "quotation_evaluations",
                schema: "evaluation_simulation");

            migrationBuilder.DropTable(
                name: "simulation_quotation_lines",
                schema: "evaluation_simulation");

            migrationBuilder.DropTable(
                name: "simulation_request_items",
                schema: "evaluation_simulation");

            migrationBuilder.DropTable(
                name: "simulation_quotation_snapshots",
                schema: "evaluation_simulation");

            migrationBuilder.DropTable(
                name: "simulation_request_snapshots",
                schema: "evaluation_simulation");

            migrationBuilder.DropTable(
                name: "simulation_runs",
                schema: "evaluation_simulation");

            migrationBuilder.DropTable(
                name: "evaluation_scenarios",
                schema: "evaluation_simulation");
        }
    }
}
