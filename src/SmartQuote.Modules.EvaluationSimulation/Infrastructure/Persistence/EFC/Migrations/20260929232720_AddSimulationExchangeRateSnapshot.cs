using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartQuote.Modules.EvaluationSimulation.Infrastructure.Persistence.EFC.Migrations
{
    /// <inheritdoc />
    public partial class AddSimulationExchangeRateSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "simulation_exchange_rate_snapshots",
                schema: "evaluation_simulation",
                columns: table => new
                {
                    simulation_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_currency = table.Column<string>(type: "char(3)", nullable: false),
                    target_currency = table.Column<string>(type: "char(3)", nullable: false),
                    rate = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    rate_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    published_on = table.Column<DateOnly>(type: "date", nullable: false),
                    source = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    retrieved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_simulation_exchange_rate_snapshots", x => x.simulation_run_id);
                    table.ForeignKey(
                        name: "fk_simulation_exchange_rate_snapshots_simulation_runs_simulati~",
                        column: x => x.simulation_run_id,
                        principalSchema: "evaluation_simulation",
                        principalTable: "simulation_runs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "simulation_exchange_rate_snapshots",
                schema: "evaluation_simulation");
        }
    }
}
