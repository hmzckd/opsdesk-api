using Microsoft.EntityFrameworkCore;
using OpsDesk.Domain.Entities;
using OpsDesk.Infrastructure.Persistence.Entities;

namespace OpsDesk.Infrastructure.Persistence;

public sealed class OpsDeskDbContext : DbContext
{
    public OpsDeskDbContext(
        DbContextOptions<OpsDeskDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<UserExternalIdentity> UserExternalIdentities => Set<UserExternalIdentity>();

    public DbSet<PasswordRecoveryJob> PasswordRecoveryJobs => Set<PasswordRecoveryJob>();
    public DbSet<EmailVerificationJob> EmailVerificationJobs => Set<EmailVerificationJob>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();

    public DbSet<UserInvitation> UserInvitations => Set<UserInvitation>();

    public DbSet<Ticket> Tickets => Set<Ticket>();

    public DbSet<SlaPolicy> SlaPolicies => Set<SlaPolicy>();

    public DbSet<TicketSlaBreach> TicketSlaBreaches =>
        Set<TicketSlaBreach>();

    public DbSet<TicketComment> TicketComments => Set<TicketComment>();

    public DbSet<TicketStatusChange> TicketStatusChanges =>
        Set<TicketStatusChange>();

    public DbSet<TicketAssignmentChange> TicketAssignmentChanges =>
        Set<TicketAssignmentChange>();

    public DbSet<EmailVerificationToken> EmailVerificationTokens =>
        Set<EmailVerificationToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(OpsDeskDbContext).Assembly);
    }
}
