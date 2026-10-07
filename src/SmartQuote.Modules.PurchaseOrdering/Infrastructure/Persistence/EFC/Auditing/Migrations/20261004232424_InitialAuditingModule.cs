using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartQuote.API.PurchaseOrdering.Infrastructure.Persistence.EFC.Auditing.Migrations
{
    /// <inheritdoc />
    public partial class InitialAuditingModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "auditing");

            migrationBuilder.CreateTable(
                name: "audit_events",
                schema: "auditing",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    entity_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_events", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_audit_events_entity_type_entity_id_occurred_at",
                schema: "auditing",
                table: "audit_events",
                columns: new[] { "entity_type", "entity_id", "occurred_at" });

            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION auditing.reject_audit_mutation()
                RETURNS trigger AS $$
                BEGIN
                    RAISE EXCEPTION 'audit events are immutable';
                END;
                $$ LANGUAGE plpgsql;

                CREATE TRIGGER audit_events_reject_update_delete
                    BEFORE UPDATE OR DELETE ON auditing.audit_events
                    FOR EACH ROW EXECUTE FUNCTION auditing.reject_audit_mutation();

                CREATE TRIGGER audit_events_reject_truncate
                    BEFORE TRUNCATE ON auditing.audit_events
                    FOR EACH STATEMENT EXECUTE FUNCTION auditing.reject_audit_mutation();
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DROP TRIGGER IF EXISTS audit_events_reject_truncate ON auditing.audit_events;
                DROP TRIGGER IF EXISTS audit_events_reject_update_delete ON auditing.audit_events;
                DROP FUNCTION IF EXISTS auditing.reject_audit_mutation();
            ");

            migrationBuilder.DropTable(
                name: "audit_events",
                schema: "auditing");
        }
    }
}
