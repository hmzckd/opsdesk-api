using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpsDesk.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPasswordRecoveryQueue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "password_recovery_jobs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    requested_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    available_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    lease_id = table.Column<Guid>(type: "uuid", nullable: true),
                    attempts = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_password_recovery_jobs", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_password_recovery_jobs_available_at_utc_id",
                table: "password_recovery_jobs",
                columns: new[] { "available_at_utc", "id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "password_recovery_jobs");
        }
    }
}
