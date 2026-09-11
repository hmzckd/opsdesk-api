using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpsDesk.Domain.Entities;
using OpsDesk.Domain.Enums;

namespace OpsDesk.Infrastructure.Persistence.Configurations;

public sealed class SlaPolicyConfiguration :
    IEntityTypeConfiguration<SlaPolicy>
{
    public static readonly Guid LowPolicyId =
        Guid.Parse("2a12b536-55b3-48e3-a739-87f7614f3692");

    public static readonly Guid MediumPolicyId =
        Guid.Parse("49eb7a75-aac7-483c-b932-c07de912f92c");

    public static readonly Guid HighPolicyId =
        Guid.Parse("97c66820-ae7e-4575-8f9f-90b93522827c");

    public static readonly Guid UrgentPolicyId =
        Guid.Parse("cd36040a-fd90-4e93-897c-b7854f05f20b");

    /// <summary>
    /// Maps SLA policies and seeds the four approved active defaults.
    /// </summary>
    public void Configure(EntityTypeBuilder<SlaPolicy> builder)
    {
        builder.ToTable("sla_policies");

        builder.HasKey(policy => policy.Id);

        builder.Property(policy => policy.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(policy => policy.Priority)
            .HasColumnName("priority")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(policy => policy.ResolutionDurationMinutes)
            .HasColumnName("resolution_duration_minutes")
            .IsRequired();

        builder.Property(policy => policy.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        builder.HasIndex(policy => policy.Priority)
            .IsUnique()
            .HasFilter("is_active = TRUE")
            .HasDatabaseName("ux_sla_policies_active_priority");

        builder.HasData(
            new
            {
                Id = LowPolicyId,
                Priority = TicketPriority.Low,
                ResolutionDurationMinutes = 7 * 24 * 60,
                IsActive = true
            },
            new
            {
                Id = MediumPolicyId,
                Priority = TicketPriority.Medium,
                ResolutionDurationMinutes = 4 * 24 * 60,
                IsActive = true
            },
            new
            {
                Id = HighPolicyId,
                Priority = TicketPriority.High,
                ResolutionDurationMinutes = 2 * 24 * 60,
                IsActive = true
            },
            new
            {
                Id = UrgentPolicyId,
                Priority = TicketPriority.Urgent,
                ResolutionDurationMinutes = 24 * 60,
                IsActive = true
            });
    }
}
