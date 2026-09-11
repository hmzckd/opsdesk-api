using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace OpsDesk.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketSlaDeadlines : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "sla_deadline_utc",
                table: "tickets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "sla_policy_id",
                table: "tickets",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "sla_policies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    priority = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    resolution_duration_minutes = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sla_policies", x => x.id);
                });

            migrationBuilder.InsertData(
                table: "sla_policies",
                columns: new[] { "id", "is_active", "priority", "resolution_duration_minutes" },
                values: new object[,]
                {
                    { new Guid("2a12b536-55b3-48e3-a739-87f7614f3692"), true, "Low", 10080 },
                    { new Guid("49eb7a75-aac7-483c-b932-c07de912f92c"), true, "Medium", 5760 },
                    { new Guid("97c66820-ae7e-4575-8f9f-90b93522827c"), true, "High", 2880 },
                    { new Guid("cd36040a-fd90-4e93-897c-b7854f05f20b"), true, "Urgent", 1440 }
                });

            migrationBuilder.Sql(
                """
                UPDATE tickets
                SET
                    sla_policy_id = CASE priority
                        WHEN 'Low' THEN '2a12b536-55b3-48e3-a739-87f7614f3692'::uuid
                        WHEN 'Medium' THEN '49eb7a75-aac7-483c-b932-c07de912f92c'::uuid
                        WHEN 'High' THEN '97c66820-ae7e-4575-8f9f-90b93522827c'::uuid
                        WHEN 'Urgent' THEN 'cd36040a-fd90-4e93-897c-b7854f05f20b'::uuid
                    END,
                    sla_deadline_utc = created_at_utc + CASE priority
                        WHEN 'Low' THEN INTERVAL '7 days'
                        WHEN 'Medium' THEN INTERVAL '4 days'
                        WHEN 'High' THEN INTERVAL '2 days'
                        WHEN 'Urgent' THEN INTERVAL '24 hours'
                    END;
                """);

            migrationBuilder.AlterColumn<DateTime>(
                name: "sla_deadline_utc",
                table: "tickets",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "sla_policy_id",
                table: "tickets",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_tickets_sla_deadline_utc",
                table: "tickets",
                column: "sla_deadline_utc");

            migrationBuilder.CreateIndex(
                name: "ix_tickets_sla_policy_id",
                table: "tickets",
                column: "sla_policy_id");

            migrationBuilder.CreateIndex(
                name: "ux_sla_policies_active_priority",
                table: "sla_policies",
                column: "priority",
                unique: true,
                filter: "is_active = TRUE");

            migrationBuilder.AddForeignKey(
                name: "fk_tickets_sla_policy",
                table: "tickets",
                column: "sla_policy_id",
                principalTable: "sla_policies",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_tickets_sla_policy",
                table: "tickets");

            migrationBuilder.DropTable(
                name: "sla_policies");

            migrationBuilder.DropIndex(
                name: "ix_tickets_sla_deadline_utc",
                table: "tickets");

            migrationBuilder.DropIndex(
                name: "ix_tickets_sla_policy_id",
                table: "tickets");

            migrationBuilder.DropColumn(
                name: "sla_deadline_utc",
                table: "tickets");

            migrationBuilder.DropColumn(
                name: "sla_policy_id",
                table: "tickets");
        }
    }
}

#pragma warning restore CA1814
