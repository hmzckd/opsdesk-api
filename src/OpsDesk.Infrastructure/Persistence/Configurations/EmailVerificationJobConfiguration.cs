using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpsDesk.Infrastructure.Persistence.Entities;

namespace OpsDesk.Infrastructure.Persistence.Configurations;

public sealed class EmailVerificationJobConfiguration :
    IEntityTypeConfiguration<EmailVerificationJob>
{
    // Maps durable resend requests without storing verification credentials.
    public void Configure(EntityTypeBuilder<EmailVerificationJob> builder)
    {
        builder.ToTable("email_verification_jobs");
        builder.HasKey(job => job.Id);
        builder.Property(job => job.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(job => job.Email).HasColumnName("email").HasMaxLength(320).IsRequired();
        builder.Property(job => job.RequestedAtUtc).HasColumnName("requested_at_utc");
        builder.Property(job => job.AvailableAtUtc).HasColumnName("available_at_utc");
        builder.Property(job => job.LeaseId).HasColumnName("lease_id");
        builder.Property(job => job.Attempts).HasColumnName("attempts");
        builder.HasIndex(job => new { job.AvailableAtUtc, job.Id });
    }
}
