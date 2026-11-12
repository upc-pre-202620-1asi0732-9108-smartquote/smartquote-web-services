using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartQuote.Modules.EvaluationSimulation.Infrastructure.Persistence.EFC.Migrations
{
    /// <inheritdoc />
    public partial class TrackTechnicalRevisions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "technical_revision",
                schema: "evaluation_simulation",
                table: "simulation_request_snapshots",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "technical_revision",
                schema: "evaluation_simulation",
                table: "evaluation_scenarios",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "technical_revision",
                schema: "evaluation_simulation",
                table: "simulation_request_snapshots");

            migrationBuilder.DropColumn(
                name: "technical_revision",
                schema: "evaluation_simulation",
                table: "evaluation_scenarios");
        }
    }
}
