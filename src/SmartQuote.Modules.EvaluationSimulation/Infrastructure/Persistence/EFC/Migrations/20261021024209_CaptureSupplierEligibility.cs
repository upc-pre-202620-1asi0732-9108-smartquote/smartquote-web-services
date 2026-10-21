using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartQuote.Modules.EvaluationSimulation.Infrastructure.Persistence.EFC.Migrations
{
    /// <inheritdoc />
    public partial class CaptureSupplierEligibility : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "supplier_account_id",
                schema: "evaluation_simulation",
                table: "simulation_quotation_snapshots",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "supplier_eligibility_status",
                schema: "evaluation_simulation",
                table: "simulation_quotation_snapshots",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "supplier_eligibility_version",
                schema: "evaluation_simulation",
                table: "simulation_quotation_snapshots",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AlterColumn<Guid>(
                name: "evaluation_criterion_id",
                schema: "evaluation_simulation",
                table: "exclusion_reasons",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM evaluation_simulation.exclusion_reasons WHERE evaluation_criterion_id IS NULL) THEN
                        RAISE EXCEPTION 'Cannot downgrade supplier eligibility while business-rule exclusions without a criterion exist. Preserve or migrate their history first.';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropColumn(
                name: "supplier_account_id",
                schema: "evaluation_simulation",
                table: "simulation_quotation_snapshots");

            migrationBuilder.DropColumn(
                name: "supplier_eligibility_status",
                schema: "evaluation_simulation",
                table: "simulation_quotation_snapshots");

            migrationBuilder.DropColumn(
                name: "supplier_eligibility_version",
                schema: "evaluation_simulation",
                table: "simulation_quotation_snapshots");

            migrationBuilder.AlterColumn<Guid>(
                name: "evaluation_criterion_id",
                schema: "evaluation_simulation",
                table: "exclusion_reasons",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
