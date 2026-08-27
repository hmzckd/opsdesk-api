using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpsDesk.Domain.Entities;

namespace OpsDesk.Infrastructure.Persistence.Configurations;

public sealed class TicketAssignmentChangeConfiguration :
    IEntityTypeConfiguration<TicketAssignmentChange>
{
    /// <summary>
    /// Maps Ticket assignment activity and its relationships to PostgreSQL.
    /// </summary>
    public void Configure(
        EntityTypeBuilder<TicketAssignmentChange> builder)
    {
        builder.ToTable("ticket_assignment_changes");

        builder.HasKey(change => change.Id);

        builder.Property(change => change.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(change => change.TicketId)
            .HasColumnName("ticket_id")
            .IsRequired();

        builder.Property(change => change.ActorId)
            .HasColumnName("actor_id")
            .IsRequired();

        builder.Property(change => change.PreviousAssigneeId)
            .HasColumnName("previous_assignee_id");

        builder.Property(change => change.NewAssigneeId)
            .HasColumnName("new_assignee_id");

        builder.Property(change => change.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasOne<Ticket>()
            .WithMany()
            .HasForeignKey(change => change.TicketId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName(
                "fk_ticket_assignment_changes_ticket");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(change => change.ActorId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName(
                "fk_ticket_assignment_changes_actor");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(change => change.PreviousAssigneeId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName(
                "fk_ticket_assignment_changes_previous_assignee");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(change => change.NewAssigneeId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName(
                "fk_ticket_assignment_changes_new_assignee");

        builder.HasIndex(change => new
        {
            change.TicketId,
            change.CreatedAtUtc,
            change.Id
        })
            .HasDatabaseName(
                "ix_ticket_assignment_changes_ticket_created_id");
    }
}
