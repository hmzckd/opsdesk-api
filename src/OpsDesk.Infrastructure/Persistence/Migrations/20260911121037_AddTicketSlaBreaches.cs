using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpsDesk.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketSlaBreaches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ticket_sla_breaches",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    ticket_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sla_policy_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sla_deadline_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    detected_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ticket_sla_breaches", x => x.id);
                    table.CheckConstraint("ck_ticket_sla_breaches_detection_after_deadline", "detected_at_utc > sla_deadline_utc");
                    table.ForeignKey(
                        name: "fk_ticket_sla_breaches_sla_policy",
                        column: x => x.sla_policy_id,
                        principalTable: "sla_policies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_ticket_sla_breaches_ticket",
                        column: x => x.ticket_id,
                        principalTable: "tickets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ticket_sla_breaches_sla_policy_id",
                table: "ticket_sla_breaches",
                column: "sla_policy_id");

            migrationBuilder.CreateIndex(
                name: "ux_ticket_sla_breaches_ticket_id",
                table: "ticket_sla_breaches",
                column: "ticket_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ticket_sla_breaches");
        }
    }
}
