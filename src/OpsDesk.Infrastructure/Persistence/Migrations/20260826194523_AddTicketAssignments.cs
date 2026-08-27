using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpsDesk.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "concurrency_token",
                table: "tickets",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "ticket_assignment_changes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ticket_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    previous_assignee_id = table.Column<Guid>(type: "uuid", nullable: true),
                    new_assignee_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ticket_assignment_changes", x => x.id);
                    table.ForeignKey(
                        name: "fk_ticket_assignment_changes_actor",
                        column: x => x.actor_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_ticket_assignment_changes_new_assignee",
                        column: x => x.new_assignee_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_ticket_assignment_changes_previous_assignee",
                        column: x => x.previous_assignee_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_ticket_assignment_changes_ticket",
                        column: x => x.ticket_id,
                        principalTable: "tickets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ticket_assignment_changes_actor_id",
                table: "ticket_assignment_changes",
                column: "actor_id");

            migrationBuilder.CreateIndex(
                name: "IX_ticket_assignment_changes_new_assignee_id",
                table: "ticket_assignment_changes",
                column: "new_assignee_id");

            migrationBuilder.CreateIndex(
                name: "IX_ticket_assignment_changes_previous_assignee_id",
                table: "ticket_assignment_changes",
                column: "previous_assignee_id");

            migrationBuilder.CreateIndex(
                name: "ix_ticket_assignment_changes_ticket_created_id",
                table: "ticket_assignment_changes",
                columns: new[] { "ticket_id", "created_at_utc", "id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ticket_assignment_changes");

            migrationBuilder.DropColumn(
                name: "concurrency_token",
                table: "tickets");
        }
    }
}
