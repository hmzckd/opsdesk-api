using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpsDesk.Domain.Entities;

namespace OpsDesk.Infrastructure.Persistence.Configurations;

public sealed class TicketStatusChangeConfiguration :
    IEntityTypeConfiguration<TicketStatusChange>
{
    /// <summary>
    /// Maps user-visible Ticket status history to PostgreSQL.
    /// </summary>
    public void Configure(
        EntityTypeBuilder<TicketStatusChange> builder)
    {
        builder.ToTable("ticket_status_changes");

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

        builder.Property(change => change.PreviousStatus)
            .HasColumnName("previous_status")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(change => change.NewStatus)
            .HasColumnName("new_status")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(change => change.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasOne<Ticket>()
            .WithMany()
            .HasForeignKey(change => change.TicketId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_ticket_status_changes_ticket");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(change => change.ActorId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_ticket_status_changes_actor");

        builder.HasIndex(change => new
        {
            change.TicketId,
            change.CreatedAtUtc,
            change.Id
        })
            .HasDatabaseName(
                "ix_ticket_status_changes_ticket_created_id");
    }
}
