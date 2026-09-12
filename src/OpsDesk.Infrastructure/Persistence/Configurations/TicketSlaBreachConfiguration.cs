using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpsDesk.Domain.Entities;

namespace OpsDesk.Infrastructure.Persistence.Configurations;

public sealed class TicketSlaBreachConfiguration :
    IEntityTypeConfiguration<TicketSlaBreach>
{
    /// <summary>
    /// Maps durable SLA breach facts and their uniqueness guarantee.
    /// </summary>
    public void Configure(EntityTypeBuilder<TicketSlaBreach> builder)
    {
        builder.ToTable(
            "ticket_sla_breaches",
            table => table.HasCheckConstraint(
                "ck_ticket_sla_breaches_detection_after_deadline",
                "detected_at_utc > sla_deadline_utc"));

        builder.HasKey(breach => breach.Id);

        builder.Property(breach => breach.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()")
            .ValueGeneratedOnAdd();

        builder.Property(breach => breach.TicketId)
            .HasColumnName("ticket_id")
            .IsRequired();

        builder.Property(breach => breach.SlaPolicyId)
            .HasColumnName("sla_policy_id")
            .IsRequired();

        builder.Property(breach => breach.SlaDeadlineUtc)
            .HasColumnName("sla_deadline_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(breach => breach.DetectedAtUtc)
            .HasColumnName("detected_at_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasOne<Ticket>()
            .WithMany()
            .HasForeignKey(breach => breach.TicketId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_ticket_sla_breaches_ticket");

        builder.HasOne<SlaPolicy>()
            .WithMany()
            .HasForeignKey(breach => breach.SlaPolicyId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_ticket_sla_breaches_sla_policy");

        builder.HasIndex(breach => breach.TicketId)
            .IsUnique()
            .HasDatabaseName("ux_ticket_sla_breaches_ticket_id");
    }
}
