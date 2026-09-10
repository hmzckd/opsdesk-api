using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using OpsDesk.Domain.Entities;
using OpsDesk.Infrastructure.Persistence;

namespace OpsDesk.Tests.Integration;

public sealed class EmailVerificationPersistenceModelTests
{
    /// <summary>
    /// Verifies EF Core maps verification state and token constraints.
    /// </summary>
    [Fact]
    public void Model_should_map_email_verification_persistence()
    {
        var options =
            new DbContextOptionsBuilder<OpsDeskDbContext>()
                .UseNpgsql(
                    "Host=localhost;Database=opsdesk_model_tests;" +
                    "Username=postgres;Password=postgres")
                .Options;

        using var dbContext = new OpsDeskDbContext(options);

        IEntityType userEntity =
            dbContext.Model.FindEntityType(typeof(User))
            ?? throw new InvalidOperationException(
                "User mapping was not found.");

        StoreObjectIdentifier usersTable =
            StoreObjectIdentifier.Table("users", null);

        IProperty emailVerifiedProperty =
            userEntity.FindProperty(
                nameof(User.EmailVerifiedAtUtc))
            ?? throw new InvalidOperationException(
                "Email verification property was not mapped.");

        Assert.Equal(
            "email_verified_at_utc",
            emailVerifiedProperty.GetColumnName(usersTable));
        Assert.True(emailVerifiedProperty.IsNullable);

        IEntityType tokenEntity =
            dbContext.Model.FindEntityType(
                typeof(EmailVerificationToken))
            ?? throw new InvalidOperationException(
                "Email verification token mapping was not found.");

        Assert.Equal(
            "email_verification_tokens",
            tokenEntity.GetTableName());

        IProperty tokenHashProperty =
            tokenEntity.FindProperty(
                nameof(EmailVerificationToken.TokenHash))
            ?? throw new InvalidOperationException(
                "Token hash property was not mapped.");

        Assert.Equal(64, tokenHashProperty.GetMaxLength());

        Assert.Contains(
            tokenEntity.GetIndexes(),
            index =>
                index.IsUnique &&
                index.Properties.Count == 1 &&
                index.Properties[0].Name ==
                    nameof(EmailVerificationToken.UserId));

        Assert.Contains(
            tokenEntity.GetIndexes(),
            index =>
                index.IsUnique &&
                index.Properties.Count == 1 &&
                index.Properties[0].Name ==
                    nameof(EmailVerificationToken.TokenHash));

        IForeignKey userForeignKey =
            Assert.Single(tokenEntity.GetForeignKeys());

        Assert.Equal(
            typeof(User),
            userForeignKey.PrincipalEntityType.ClrType);
        Assert.Equal(
            DeleteBehavior.Cascade,
            userForeignKey.DeleteBehavior);
    }
}
