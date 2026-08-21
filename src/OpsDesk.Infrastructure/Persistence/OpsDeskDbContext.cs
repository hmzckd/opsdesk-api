using Microsoft.EntityFrameworkCore;
using OpsDesk.Domain.Entities;

namespace OpsDesk.Infrastructure.Persistence;

public sealed class OpsDeskDbContext : DbContext
{
    public OpsDeskDbContext(
        DbContextOptions<OpsDeskDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();

    public DbSet<Ticket> Tickets => Set<Ticket>();

    public DbSet<TicketStatusChange> TicketStatusChanges =>
        Set<TicketStatusChange>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(OpsDeskDbContext).Assembly);
    }

}
