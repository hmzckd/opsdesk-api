using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpsDesk.Domain.Entities;

namespace OpsDesk.Infrastructure.Persistence.Configurations;

public sealed class TicketCommentConfiguration :
    IEntityTypeConfiguration<TicketComment>
{
    /// <summary>
    /// Maps public Ticket Comments and their relationships to PostgreSQL.
    /// </summary>
    public void Configure(EntityTypeBuilder<TicketComment> builder)
    {
        builder.ToTable("ticket_comments");

        builder.HasKey(comment => comment.Id);

        builder.Property(comment => comment.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(comment => comment.TicketId)
            .HasColumnName("ticket_id")
            .IsRequired();

        builder.Property(comment => comment.AuthorId)
            .HasColumnName("author_id")
            .IsRequired();

        builder.Property(comment => comment.Content)
            .HasColumnName("content")
            .HasMaxLength(TicketComment.MaximumContentLength)
            .IsRequired();

        builder.Property(comment => comment.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasOne<Ticket>()
            .WithMany()
            .HasForeignKey(comment => comment.TicketId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_ticket_comments_ticket");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(comment => comment.AuthorId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_ticket_comments_author");

        builder.HasIndex(comment => new
        {
            comment.TicketId,
            comment.CreatedAtUtc,
            comment.Id
        })
            .HasDatabaseName(
                "ix_ticket_comments_ticket_created_id");
    }
}
