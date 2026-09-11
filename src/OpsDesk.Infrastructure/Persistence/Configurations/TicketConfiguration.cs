using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpsDesk.Domain.Entities;

namespace OpsDesk.Infrastructure.Persistence.Configurations;

public sealed class TicketConfiguration :
    IEntityTypeConfiguration<Ticket>
{
    /// <summary>
    /// Maps the Ticket entity and its relationships to PostgreSQL.
    /// </summary>
    public void Configure(EntityTypeBuilder<Ticket> builder)
    {
        builder.ToTable("tickets");

        builder.HasKey(ticket => ticket.Id);

        builder.Property(ticket => ticket.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(ticket => ticket.Title)
            .HasColumnName("title")
            .HasMaxLength(Ticket.MaximumTitleLength)
            .IsRequired();

        builder.Property(ticket => ticket.Description)
            .HasColumnName("description")
            .HasMaxLength(Ticket.MaximumDescriptionLength)
            .IsRequired();

        builder.Property(ticket => ticket.Priority)
            .HasColumnName("priority")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(ticket => ticket.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(ticket => ticket.RequesterId)
            .HasColumnName("requester_id")
            .IsRequired();

        builder.Property(ticket => ticket.AssigneeId)
            .HasColumnName("assignee_id");

        builder.Property(ticket => ticket.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(ticket => ticket.UpdatedAtUtc)
            .HasColumnName("updated_at_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(ticket => ticket.ResolvedAtUtc)
            .HasColumnName("resolved_at_utc")
            .HasColumnType("timestamp with time zone");

        builder.Property(ticket => ticket.ClosedAtUtc)
            .HasColumnName("closed_at_utc")
            .HasColumnType("timestamp with time zone");

        builder.Property(ticket => ticket.SlaPolicyId)
            .HasColumnName("sla_policy_id")
            .IsRequired();

        builder.Property(ticket => ticket.SlaDeadlineUtc)
            .HasColumnName("sla_deadline_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(ticket => ticket.ConcurrencyToken)
            .HasColumnName("concurrency_token")
            .IsRequired()
            .IsConcurrencyToken();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(ticket => ticket.RequesterId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_tickets_requester");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(ticket => ticket.AssigneeId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_tickets_assignee");

        builder.HasOne<SlaPolicy>()
            .WithMany()
            .HasForeignKey(ticket => ticket.SlaPolicyId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_tickets_sla_policy");

        builder.HasIndex(ticket => ticket.RequesterId)
            .HasDatabaseName("ix_tickets_requester_id");

        builder.HasIndex(ticket => ticket.AssigneeId)
            .HasDatabaseName("ix_tickets_assignee_id");

        builder.HasIndex(ticket => ticket.SlaPolicyId)
            .HasDatabaseName("ix_tickets_sla_policy_id");

        builder.HasIndex(ticket => ticket.SlaDeadlineUtc)
            .HasDatabaseName("ix_tickets_sla_deadline_utc");
    }
}
