using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpsDesk.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInvitationAcceptance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_user_invitations_pending_email",
                table: "user_invitations");

            migrationBuilder.AddColumn<DateTime>(
                name: "accepted_at_utc",
                table: "user_invitations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "accepted_user_id",
                table: "user_invitations",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_invitations_accepted_user_id",
                table: "user_invitations",
                column: "accepted_user_id");

            migrationBuilder.CreateIndex(
                name: "ux_user_invitations_pending_email",
                table: "user_invitations",
                column: "email",
                unique: true,
                filter: "revoked_at_utc IS NULL AND accepted_at_utc IS NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_user_invitations_users_accepted_user_id",
                table: "user_invitations",
                column: "accepted_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_user_invitations_users_accepted_user_id",
                table: "user_invitations");

            migrationBuilder.DropIndex(
                name: "IX_user_invitations_accepted_user_id",
                table: "user_invitations");

            migrationBuilder.DropIndex(
                name: "ux_user_invitations_pending_email",
                table: "user_invitations");

            migrationBuilder.DropColumn(
                name: "accepted_at_utc",
                table: "user_invitations");

            migrationBuilder.DropColumn(
                name: "accepted_user_id",
                table: "user_invitations");

            migrationBuilder.CreateIndex(
                name: "ux_user_invitations_pending_email",
                table: "user_invitations",
                column: "email",
                unique: true,
                filter: "revoked_at_utc IS NULL");
        }
    }
}
