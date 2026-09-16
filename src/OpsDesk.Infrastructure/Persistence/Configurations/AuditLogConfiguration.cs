using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpsDesk.Domain.Entities;

namespace OpsDesk.Infrastructure.Persistence.Configurations;

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    /// <summary>
    /// Maps allowlisted audit fields to an append-only application table.
    /// </summary>
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_logs");
        builder.HasKey(entry => entry.Id);
        builder.Property(entry => entry.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(entry => entry.Action).HasColumnName("action")
            .HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(entry => entry.ActorId).HasColumnName("actor_id").IsRequired();
        builder.Property(entry => entry.TargetType).HasColumnName("target_type")
            .HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(entry => entry.TargetId).HasColumnName("target_id").IsRequired();
        builder.Property(entry => entry.OccurredAtUtc).HasColumnName("occurred_at_utc")
            .HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(entry => entry.PreviousStatus).HasColumnName("previous_status")
            .HasConversion<string>().HasMaxLength(30);
        builder.Property(entry => entry.NewStatus).HasColumnName("new_status")
            .HasConversion<string>().HasMaxLength(30);
        builder.Property(entry => entry.PreviousAssigneeId)
            .HasColumnName("previous_assignee_id");
        builder.Property(entry => entry.NewAssigneeId)
            .HasColumnName("new_assignee_id");

        builder.HasOne<User>().WithMany()
            .HasForeignKey(entry => entry.ActorId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_audit_logs_actor");

        builder.HasIndex(entry => new { entry.OccurredAtUtc, entry.Id })
            .IsDescending().HasDatabaseName("ix_audit_logs_occurred_id");
        builder.HasIndex(entry => new { entry.Action, entry.OccurredAtUtc,
            entry.Id })
            .IsDescending(false, true, true)
            .HasDatabaseName("ix_audit_logs_action_occurred_id");
        builder.HasIndex(entry => new { entry.TargetType, entry.TargetId,
            entry.OccurredAtUtc, entry.Id })
            .IsDescending(false, false, true, true)
            .HasDatabaseName("ix_audit_logs_target_occurred_id");
        builder.HasIndex(entry => new { entry.ActorId, entry.OccurredAtUtc,
            entry.Id })
            .IsDescending(false, true, true)
            .HasDatabaseName("ix_audit_logs_actor_occurred_id");
    }
}
