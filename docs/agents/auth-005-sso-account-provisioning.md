# AUTH-005: SSO Account Provisioning

## Durum ve Sinir

Bu belge AUTH-005'in hesap olusturma ve veritabani dilimini aciklar.
Bu ilk checkpoint yazildiginda Keycloak, HTTP OIDC ve logout henuz tamamlanmamisti; sonraki dilim
`auth-005-sso-browser-flow.md` dosyasinda aciklanir. SSO halen varsayilan olarak kapalidir.
Migration yalnizca gecici PostgreSQL test veritabaninda uygulandi; gelistirme veritabani degistirilmedi.
Git commit, push veya tag yapilmadi.

## Akis

Gelecekteki OIDC handler -> IExternalSignInService -> davet/kimlik repository'leri -> PostgreSQL -> OpsDesk JWT.

OIDC handler, saglayicinin imzali cevabini ve state/nonce gibi protokol kontrollerini dogrulayacak.
VerifiedExternalIdentity ancak bundan sonra olusturulmali; istemciden gelen JSON dogrulanmis kimlik sayilmaz.
Bu dilimde testler dogrudan onayli Application arayuzunu cagiriyor. Bu, OIDC protokolunun test edildigi anlamina gelmez.

- Issuer: kimlik saglayicisinin tam kimligi/adresi.
- Subject: o saglayicinin kullaniciya verdigi kalici kimlik.
- Issuer + subject birlikte yerel hesabi bulur; e-posta otomatik hesap baglama anahtari degildir.
- Ilk giriste dogrulanmis e-posta ile yonetici daveti eslesir. Rol davetten gelir.
- Kullanici, dis kimlik kaydi ve davet tuketimi tek transaction icinde kaydedilir.
- Transaction: ilgili yazmalarin hepsinin birlikte basarmasi veya hicbirinin kalmamasi.
- Sonraki giris ayni yerel hesaba doner; yeni davet aranmaz. Saglayici/domain kontrolleri yine uygulanir.
- SSO hesabinin yerel PasswordHash degeri null olur; bu hesap yerel parola ile giris yapamaz.

## Metotlari Okuma

### UserExternalIdentity.Create - Domain

public static UserExternalIdentity Create(Guid userId, string issuer, string subject, DateTime createdAtUtc)

public: diger katmanlar cagirabilir. static: once bir nesne yaratmadan sinif uzerinden cagrilir.
Parametreler yeni baglantinin yerel kullanicisi, saglayicisi, dis kullanici kimligi ve UTC zamanidir.
Guid bir kimlik turudur; DateTime zaman tasir. Metot bir UserExternalIdentity DONDURUR.
Govde bos/deger sinirlarini ve UTC bilgisini kontrol eder, yeni Id uretir ve nesneyi kurar.
Veritabanina yazmaz. Domain'dedir cunku kimlik baglantisinin temel tutarlilik kurallarini korur.
Application bu nesneyi olusturur; Infrastructure daha sonra kaydeder.

### ExternalSignInService.SignInAsync - Application

public async Task<AuthResponse> SignInAsync(VerifiedExternalIdentity identity, string? invitationHash, CancellationToken cancellationToken = default)

Task<AuthResponse>, islemin asenkron bitince AuthResponse dondurecegini belirtir; AuthResponse bir parametre DEGILDIR.
async, govdede await ile veritabani islemlerinin sonucunu beklemeye izin verir.
identity dogrulanmis saglayici bilgileridir; invitationHash davet kodunun tek yonlu ozetidir.
string? bu degerin null olabilecegini belirtir: tekrar giriste davet gerekmeyebilir.
CancellationToken, cagiran istek iptal edilirse veritabani islemlerine iptal sinyali tasir; default istege baglidir.

Govde: saglayici/dogrulanmis e-posta/domain kontrolu -> kalici kimlik aramasi -> gerekiyorsa davet
kontrolu ve hesap olusturma -> JWT uretimi -> AuthResponse.
Reddedilen is kurali ForbiddenException, mevcut hesap cakismasi ConflictException olarak bildirilir.
Application'dadir cunku HTTP veya SQL ayrintisi degil, girisin hangi kosullarda basarili olacagini yonetir.
Constructor parametreleri DI tarafindan verilen bagimliliklardir; new ile kendisi veritabani veya JWT araci kurmaz.

### ExternalIdentityRepository.GetUserAsync - Infrastructure

public Task<User?> GetUserAsync(string issuer, string subject, CancellationToken cancellationToken = default)

Task<User?> sonuc bittiginde User veya null verir. User parametre degildir.
Govde issuer/subject ile dis kimligi bulup users tablosuyla birlestirir.
AsNoTracking, salt okunur sonuc icin EF'nin degisiklik takibi yapmamasini saglar.
Metot async yazmadan EF'nin hazir Task sonucunu dogrudan dondurur; cagirani yine await kullanabilir.
Infrastructure'dadir cunku sorgunun EF Core/PostgreSQL uzerindeki gercek uygulamasidir.

### ExternalIdentityRepository.TryAcceptInvitationAsync - Infrastructure

public async Task<bool> TryAcceptInvitationAsync(Guid invitationId, User user, UserExternalIdentity identity, CancellationToken cancellationToken = default)

bool sonuc: true birlikte kaydedildi; false davet artik kullanilamaz veya bilgiler eslesmiyor.
invitationId secilen davet, user yeni hesap, identity onun dis kimlik baglantisidir.
Govde transaction acar, davet satirini FOR UPDATE ile kilitler, SAAT KONTROLUNU KILITTEN SONRA tekrar yapar.
Boylece beklerken suresi dolan veya diger istegin kullandigi davet kabul edilmez.
Ardindan user + identity kaydeder, daveti kabul edildi olarak isaretler ve commit yapar.
Erken donus/hata durumunda await using transaction'i dispose ederek commit edilmemis yazmalari geri alir.
Bu metot SQL ve atomik yazma bildigi icin Infrastructure'dadir; is akisi Application'da kalir.

### UserExternalIdentityConfiguration.Configure - Infrastructure

public void Configure(EntityTypeBuilder<UserExternalIdentity> builder)

void sonuc nesnesi dondurmez. builder, EF'nin tablo eslemesini kurmak icin verdigi parametredir.
Govde tablo/sutun isimlerini, uzunluklari, foreign key'i ve issuer+subject benzersizligini tanimlar.
Foreign key, dis kimligin var olan bir users kaydina bagli olmasini saglar.
Unique index ayni saglayici kimliginin iki kez kaydedilmesini veritabani duzeyinde engeller.
Bu metot kullanici girisinde tek tek cagrilmaz; EF modelini hazirlarken kullanilir.

### Migration.Up ve Migration.Down - Infrastructure

protected override void Up(MigrationBuilder migrationBuilder)
protected override void Down(MigrationBuilder migrationBuilder)

protected: tureyen sinif/EF migration akisinin kullandigi erisim.
override: EF'nin Migration taban sinifindaki davranisi bu migration icin tanimlar.
Up yeni tabloyu olusturur ve password_hash'i nullable yapar; mevcut hash'leri silmez.
Down ters islemi tarif eder. SSO kimlik tablosunu SILDIKLERI icin rollback veri kaybina yol acabilir.
Down bu calismada calistirilmadi; production rollback otomatik uygulanmamali.

## Test Metotlari - Tests

Test sinifindaki public async Task metotlari xUnit tarafindan calistirilir; HTTP cevabi dondurmez.
[Fact] tek senaryo; [Theory] her [InlineData] satiri icin ayni senaryonun bir varyasyonudur.
Bu dosyada 5 test metodu, Theory varyasyonlariyla toplam 10 test durumu vardir.

- Invited_external_identity_should_create_one_passwordless_account_and_return_to_it:
  davetli Agent hesabi, tekrar giris, JWT ile /me ve /tickets, yerel parola reddi, davet replay reddi.
- Unapproved_identity_should_be_rejected_without_consuming_invitation(string invalidField):
  parametre InlineData'dan gelir; 6 farkli reddetme kosulu denenir, sonra dogru kimlikle davet kabul edilir.
- Concurrent_external_acceptance_should_have_one_winner:
  iki ayri istek scope'u ile ayni davete yarisir; tek kazanan ve kaybedenin tekrar giris reddi beklenir.
- Existing_local_account_should_not_be_linked_or_modified:
  ayni e-postali parola hesabi varsa SSO otomatik baglanmaz; tekrar deneme de cakisma verir, eski hesap korunur.
- External_sign_in_should_be_disabled_by_default:
  acik saglayici konfigurasyonu olmadan giris reddedilir.

Test yardimcilari:
- private CreateEnabledFactory(): sadece bu test sunucusuna guvenilen test saglayicisini enjekte eder.
- private async Task<string> InviteAsync(factory, client, email, role = "customer"):
  admin HTTP girisi yapar, gercek davet endpoint'ini cagirir, test posta kutusundan kodu alip hash'ini dondurur.
- private static async Task<AuthResponse> SignInAsync(factory, identity, invitationHash):
  her cagriya ayri DI scope/DbContext verir ve gercek Application servisini cagirir.
private yardimcilar testlerin hazirlik tekrarini azaltir; production is kuralini taklit etmez.

## TDD Kaniti

Son dogrulama: 313 test basarili, 0 basarisiz, 0 atlanan. Build 0 hata/uyari. Slopwatch 0 sorun.
Bu sayilar yalnizca mevcut kod/test kapsamini kanitlar; Keycloak tarayici akisi henuz test edilmedi.

Ilk hesap olusturma testi once servis kayitli olmadigi icin kirmiziydi; onceki turda hata goruldu.
Servis/repository ve migration eklenince PostgreSQL uzerinde gecti.
Sonradan eklenen olumsuz senaryolar mevcut korumalarla ilk calistirmada gecti; bunlar regresyon testleridir,
ayri bir kirmizi-asama kaniti varmis gibi raporlanmaz.

## Diger Dosyalardaki Degisiklikler

- src/OpsDesk.Domain/Entities/User.cs: PasswordHash string? oldu.
- src/OpsDesk.Infrastructure/Persistence/Configurations/UserConfiguration.cs: password_hash zorunlulugu kaldirildi.
- src/OpsDesk.Application/Auth/Services/AuthService.cs: hash yoksa parola dogrulayiciya null gondermek yerine giris reddedilir.
- src/OpsDesk.Infrastructure/Persistence/OpsDeskDbContext.cs: UserExternalIdentities DbSet eklendi.
- src/OpsDesk.Infrastructure/DependencyInjection.cs: IExternalIdentityRepository scoped kaydi eklendi.
- src/OpsDesk.Api/Program.cs: IExternalSignInService scoped kaydi ve kapali varsayilan politika eklendi.
- Migration Designer ve model snapshot dosyalari EF CLI tarafindan uretildi.

## Tam Kaynaklar

Asagidaki bloklar bu dilimin tam dosya icerikleridir; sonraki degisikliklerde asil kaynak dosyalar esas alinir.

### src/OpsDesk.Domain/Entities/UserExternalIdentity.cs

```csharp
namespace OpsDesk.Domain.Entities;

public sealed class UserExternalIdentity
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Issuer { get; private set; } = string.Empty;
    public string Subject { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }

    private UserExternalIdentity() { }

    // Binds a provider's stable subject to one local user; email is deliberately not the identity key.
    public static UserExternalIdentity Create(Guid userId, string issuer, string subject, DateTime createdAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(issuer);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        if (userId == Guid.Empty || issuer.Length > 512 || subject.Length > 255
            || createdAtUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("External identity requires a user, bounded issuer/subject and UTC time.");
        return new UserExternalIdentity
        {
            Id = Guid.NewGuid(), UserId = userId, Issuer = issuer, Subject = subject, CreatedAtUtc = createdAtUtc
        };
    }
}
```

### src/OpsDesk.Application/Auth/Models/VerifiedExternalIdentity.cs

```csharp
namespace OpsDesk.Application.Auth.Models;

// Only the validated OIDC handler constructs this input in production, never a public JSON endpoint.
public sealed record VerifiedExternalIdentity(
    string Issuer, string Subject, string Email, bool EmailVerified, string FirstName, string LastName);
```

### src/OpsDesk.Application/Auth/Models/ExternalSignInPolicy.cs

```csharp
namespace OpsDesk.Application.Auth.Models;

public sealed record ExternalSignInPolicy(bool Enabled, string Issuer, IReadOnlyCollection<string> AllowedEmailDomains);
```

### src/OpsDesk.Application/Auth/Interfaces/IExternalSignInService.cs

```csharp
using OpsDesk.Application.Auth.DTOs;
using OpsDesk.Application.Auth.Models;

namespace OpsDesk.Application.Auth.Interfaces;

public interface IExternalSignInService
{
    // Resolves a trusted identity or atomically accepts a matching invitation on first sign-in.
    Task<AuthResponse> SignInAsync(VerifiedExternalIdentity identity, string? invitationHash,
        CancellationToken cancellationToken = default);
}
```

### src/OpsDesk.Application/Auth/Interfaces/IExternalIdentityRepository.cs

```csharp
using OpsDesk.Domain.Entities;

namespace OpsDesk.Application.Auth.Interfaces;

public interface IExternalIdentityRepository
{
    // Resolves an established link without matching or changing any account by email.
    Task<User?> GetUserAsync(string issuer, string subject, CancellationToken cancellationToken = default);

    // Creates the account/link and consumes a still-valid matching invitation in one transaction.
    Task<bool> TryAcceptInvitationAsync(Guid invitationId, User user, UserExternalIdentity identity,
        CancellationToken cancellationToken = default);
}
```

### src/OpsDesk.Application/Auth/Services/ExternalSignInService.cs

```csharp
using OpsDesk.Application.Auth.DTOs;
using OpsDesk.Application.Auth.Interfaces;
using OpsDesk.Application.Auth.Models;
using OpsDesk.Application.Common.Exceptions;
using OpsDesk.Application.Invitations.Interfaces;
using OpsDesk.Domain.Entities;

namespace OpsDesk.Application.Auth.Services;

public sealed class ExternalSignInService(
    IExternalIdentityRepository identities, IInvitationRepository invitations,
    IJwtTokenGenerator jwt, IEmailValidator emailValidator,
    ExternalSignInPolicy policy, TimeProvider timeProvider) : IExternalSignInService
{
    // Enforces the approved provider/domain and invitation before issuing an OpsDesk JWT.
    public async Task<AuthResponse> SignInAsync(VerifiedExternalIdentity identity, string? invitationHash,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(identity);
        if (!policy.Enabled || identity.Issuer != policy.Issuer || !identity.EmailVerified
            || string.IsNullOrWhiteSpace(identity.Subject) || identity.Subject.Length > 255)
            throw new ForbiddenException("The external identity is not approved for OpsDesk.");
        emailValidator.Validate(identity.Email);
        string email = UserInputNormalizer.NormalizeEmail(identity.Email);
        string domain = email[(email.LastIndexOf('@') + 1)..];
        if (!policy.AllowedEmailDomains.Contains(domain, StringComparer.OrdinalIgnoreCase))
            throw new ForbiddenException("The external identity is not approved for OpsDesk.");

        User? user = await identities.GetUserAsync(identity.Issuer, identity.Subject, cancellationToken);
        if (user is null)
        {
            UserInvitation? invitation = string.IsNullOrWhiteSpace(invitationHash) ? null
                : await invitations.GetByTokenHashAsync(invitationHash, cancellationToken);
            DateTime nowUtc = timeProvider.GetUtcNow().UtcDateTime;
            if (invitation is null || invitation.Email != email || !invitation.CanAcceptAt(nowUtc))
                throw new ForbiddenException("A valid administrator invitation matching your verified email is required.");
            user = new User
            {
                FirstName = UserInputNormalizer.NormalizeName(identity.FirstName, nameof(identity.FirstName)),
                LastName = UserInputNormalizer.NormalizeName(identity.LastName, nameof(identity.LastName)),
                Email = email, Role = invitation.Role, PasswordHash = null, CreatedAtUtc = nowUtc
            };
            user.MarkEmailVerified(nowUtc);
            UserExternalIdentity link = UserExternalIdentity.Create(user.Id, identity.Issuer, identity.Subject, nowUtc);
            if (!await identities.TryAcceptInvitationAsync(invitation.Id, user, link, cancellationToken))
                throw new ForbiddenException("A valid administrator invitation matching your verified email is required.");
        }
        var token = jwt.GenerateToken(user);
        return new AuthResponse(user.Id, user.FirstName, user.LastName, user.Email, user.Role.ToString(),
            token.AccessToken, token.ExpiresAtUtc);
    }
}
```

### src/OpsDesk.Infrastructure/Persistence/Configurations/UserExternalIdentityConfiguration.cs

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpsDesk.Domain.Entities;

namespace OpsDesk.Infrastructure.Persistence.Configurations;

public sealed class UserExternalIdentityConfiguration : IEntityTypeConfiguration<UserExternalIdentity>
{
    // Keeps provider subjects unique and removes identity links only when their user is removed.
    public void Configure(EntityTypeBuilder<UserExternalIdentity> builder)
    {
        builder.ToTable("user_external_identities");
        builder.HasKey(identity => identity.Id);
        builder.Property(identity => identity.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(identity => identity.UserId).HasColumnName("user_id");
        builder.Property(identity => identity.Issuer).HasColumnName("issuer").HasMaxLength(512).IsRequired();
        builder.Property(identity => identity.Subject).HasColumnName("subject").HasMaxLength(255).IsRequired();
        builder.Property(identity => identity.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.HasIndex(identity => new { identity.Issuer, identity.Subject }).IsUnique()
            .HasDatabaseName("ux_user_external_identities_issuer_subject");
        builder.HasOne<User>().WithMany().HasForeignKey(identity => identity.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
```

### src/OpsDesk.Infrastructure/Persistence/Repositories/ExternalIdentityRepository.cs

```csharp
using Microsoft.EntityFrameworkCore;
using Npgsql;
using OpsDesk.Application.Auth.Interfaces;
using OpsDesk.Application.Common.Exceptions;
using OpsDesk.Domain.Entities;

namespace OpsDesk.Infrastructure.Persistence.Repositories;

public sealed class ExternalIdentityRepository(OpsDeskDbContext database, TimeProvider timeProvider) : IExternalIdentityRepository
{
    // Joins by the stable provider key, never by email, and keeps the returned user read-only.
    public Task<User?> GetUserAsync(string issuer, string subject, CancellationToken cancellationToken = default)
    {
        return database.UserExternalIdentities.Where(identity => identity.Issuer == issuer && identity.Subject == subject)
            .Join(database.Users, identity => identity.UserId, user => user.Id, (_, user) => user)
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
    }

    // Rechecks expiry after locking the invitation, then commits user, link and consumption together.
    public async Task<bool> TryAcceptInvitationAsync(Guid invitationId, User user, UserExternalIdentity identity,
        CancellationToken cancellationToken = default)
    {
        if (identity.UserId != user.Id) throw new ArgumentException("External identity user does not match.");
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        List<UserInvitation> invitations = await database.UserInvitations.FromSqlInterpolated(
            $"SELECT * FROM user_invitations WHERE id = {invitationId} FOR UPDATE")
            .AsNoTracking().ToListAsync(cancellationToken);
        UserInvitation? invitation = invitations.SingleOrDefault();
        DateTime nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        if (invitation is null || invitation.Email != user.Email || invitation.Role != user.Role || !invitation.CanAcceptAt(nowUtc))
            return false;
        database.Users.Add(user);
        database.UserExternalIdentities.Add(identity);
        try
        {
            await database.SaveChangesAsync(cancellationToken);
            await database.UserInvitations.Where(row => row.Id == invitationId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(row => row.AcceptedAtUtc, nowUtc)
                    .SetProperty(row => row.AcceptedUserId, user.Id), cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "ux_users_email" or "ux_user_external_identities_issuer_subject"
        })
        {
            throw new ConflictException("An account or external identity already exists. Automatic account linking is not allowed.", exception);
        }
    }
}
```

### src/OpsDesk.Infrastructure/Persistence/Migrations/20260907152235_AddExternalUserIdentities.cs

```csharp
using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpsDesk.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddExternalUserIdentities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "password_hash",
                table: "users",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500);

            migrationBuilder.CreateTable(
                name: "user_external_identities",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    issuer = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    subject = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_external_identities", x => x.id);
                    table.ForeignKey(
                        name: "FK_user_external_identities_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_user_external_identities_user_id",
                table: "user_external_identities",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ux_user_external_identities_issuer_subject",
                table: "user_external_identities",
                columns: new[] { "issuer", "subject" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_external_identities");

            migrationBuilder.AlterColumn<string>(
                name: "password_hash",
                table: "users",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);
        }
    }
}
```

### tests/OpsDesk.Tests/Integration/ExternalSignInIntegrationTests.cs

```csharp
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.Mvc.Testing;
using OpsDesk.Application.Auth.DTOs;
using OpsDesk.Application.Auth.Interfaces;
using OpsDesk.Application.Auth.Models;
using OpsDesk.Application.Common.Exceptions;
using OpsDesk.Application.Invitations.Interfaces;

namespace OpsDesk.Tests.Integration;

[Collection(IntegrationTestCollection.Name)]
public sealed class ExternalSignInIntegrationTests(OpsDeskApiFixture fixture)
{
    private const string Issuer = "https://identity.opsdesk.test/realms/opsdesk";

    // A rejected identity must not consume the invitation that its real owner can still accept.
    [Theory]
    [InlineData("issuer")]
    [InlineData("unverified")]
    [InlineData("domain")]
    [InlineData("email")]
    [InlineData("subject")]
    [InlineData("invitation")]
    public async Task Unapproved_identity_should_be_rejected_without_consuming_invitation(string invalidField)
    {
        using var factory = CreateEnabledFactory();
        using HttpClient client = factory.CreateClient();
        string email = $"sso-denied-{Guid.NewGuid():N}@example.com";
        string hash = await InviteAsync(factory, client, email);
        var valid = new VerifiedExternalIdentity(Issuer, Guid.NewGuid().ToString(), email, true, "SSO", "User");
        VerifiedExternalIdentity invalid = invalidField switch
        {
            "issuer" => valid with { Issuer = "https://unapproved.opsdesk.test/realms/opsdesk" },
            "unverified" => valid with { EmailVerified = false },
            "domain" => valid with { Email = "person@unapproved.example" },
            "email" => valid with { Email = $"other-{Guid.NewGuid():N}@example.com" },
            "subject" => valid with { Subject = " " },
            _ => valid
        };

        await Assert.ThrowsAsync<ForbiddenException>(() => SignInAsync(factory, invalid,
            invalidField == "invitation" ? null : hash));
        AuthResponse accepted = await SignInAsync(factory, valid, hash);
        Assert.Equal(email, accepted.Email);
        Assert.Equal("Customer", accepted.Role);
    }

    // Competing identities cannot both consume the same invitation, even in separate requests.
    [Fact]
    public async Task Concurrent_external_acceptance_should_have_one_winner()
    {
        using var factory = CreateEnabledFactory();
        using HttpClient client = factory.CreateClient();
        string email = $"sso-race-{Guid.NewGuid():N}@example.com";
        string hash = await InviteAsync(factory, client, email);
        var first = new VerifiedExternalIdentity(Issuer, Guid.NewGuid().ToString(), email, true, "First", "User");
        var second = first with { Subject = Guid.NewGuid().ToString(), FirstName = "Second" };
        Task<AuthResponse>[] attempts = [SignInAsync(factory, first, hash), SignInAsync(factory, second, hash)];
        Exception? error = await Record.ExceptionAsync(() => Task.WhenAll(attempts));

        Assert.IsType<ForbiddenException>(error);
        Task<AuthResponse> winner = Assert.Single(attempts, attempt => attempt.IsCompletedSuccessfully);
        _ = Assert.Single(attempts, attempt => attempt.IsFaulted);
        VerifiedExternalIdentity winningIdentity = winner == attempts[0] ? first : second;
        VerifiedExternalIdentity losingIdentity = winner == attempts[0] ? second : first;
        AuthResponse returning = await SignInAsync(factory, winningIdentity, null);
        AuthResponse created = await winner;
        Assert.Equal(created.UserId, returning.UserId);
        await Assert.ThrowsAsync<ForbiddenException>(() => SignInAsync(factory, losingIdentity, null));
    }

    // Matching email alone must not link or overwrite an existing local account, and failures roll back.
    [Fact]
    public async Task Existing_local_account_should_not_be_linked_or_modified()
    {
        using var factory = CreateEnabledFactory();
        using HttpClient client = factory.CreateClient();
        string email = $"sso-existing-{Guid.NewGuid():N}@example.com";
        string hash = await InviteAsync(factory, client, email, "agent");
        using HttpResponseMessage registration = await client.PostAsJsonAsync("/auth/register",
            new RegisterRequest("Original", "Person", email, "OriginalPass!"));
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
        AuthResponse original = Assert.IsType<AuthResponse>(await registration.Content.ReadFromJsonAsync<AuthResponse>());
        var identity = new VerifiedExternalIdentity(Issuer, Guid.NewGuid().ToString(), email, true, "SSO", "Agent");

        await Assert.ThrowsAsync<ConflictException>(() => SignInAsync(factory, identity, hash));
        await Assert.ThrowsAsync<ConflictException>(() => SignInAsync(factory, identity, hash));
        await Assert.ThrowsAsync<ForbiddenException>(() => SignInAsync(factory, identity, null));

        using HttpResponseMessage login = await client.PostAsJsonAsync("/auth/login",
            new LoginRequest(email, "OriginalPass!"));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        AuthResponse unchanged = Assert.IsType<AuthResponse>(await login.Content.ReadFromJsonAsync<AuthResponse>());
        Assert.Equal(original.UserId, unchanged.UserId);
        Assert.Equal("Original", unchanged.FirstName);
        Assert.Equal("Customer", unchanged.Role);
    }

    // Without explicit provider configuration, even a plausible verified identity is rejected.
    [Fact]
    public async Task External_sign_in_should_be_disabled_by_default()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        var identity = new VerifiedExternalIdentity(Issuer, Guid.NewGuid().ToString(),
            "disabled@example.com", true, "SSO", "User");

        await Assert.ThrowsAsync<ForbiddenException>(() => SignInAsync(fixture.Factory, identity, null));
    }

    // Each host gets an explicit trusted test provider; production remains disabled by default.
    private WebApplicationFactory<Program> CreateEnabledFactory()
    {
        return fixture.Factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<ExternalSignInPolicy>();
            services.AddSingleton(new ExternalSignInPolicy(true, Issuer, ["example.com"]));
        }));
    }

    // Creates a real administrator invitation through HTTP and returns only its stored-token hash.
    private async Task<string> InviteAsync(WebApplicationFactory<Program> factory, HttpClient client,
        string email, string role = "customer")
    {
        using HttpResponseMessage login = await client.PostAsJsonAsync("/auth/login",
            new LoginRequest(OpsDeskApiFactory.AdminEmail, OpsDeskApiFactory.AdminPassword));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        AuthResponse admin = Assert.IsType<AuthResponse>(await login.Content.ReadFromJsonAsync<AuthResponse>());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", admin.AccessToken);
        using HttpResponseMessage invited = await client.PostAsJsonAsync("/admin/invitations", new { email, role });
        Assert.Equal(HttpStatusCode.Created, invited.StatusCode);
        client.DefaultRequestHeaders.Authorization = null;
        string raw = Assert.Single(fixture.Factory.SentInvitations, message => message.RecipientEmail == email).RawToken;
        using IServiceScope scope = factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IInvitationTokenGenerator>().ComputeHash(raw);
    }

    // Separate scopes model separate requests and prevent accidental shared DbContext state in races.
    private static async Task<AuthResponse> SignInAsync(WebApplicationFactory<Program> factory,
        VerifiedExternalIdentity identity, string? invitationHash)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IExternalSignInService>()
            .SignInAsync(identity, invitationHash);
    }

    // The invitation owns the role; the resulting identity can return without another invitation or local password.
    [Fact]
    public async Task Invited_external_identity_should_create_one_passwordless_account_and_return_to_it()
    {
        using var factory = fixture.Factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<ExternalSignInPolicy>();
            services.AddSingleton(new ExternalSignInPolicy(true, Issuer, ["example.com"]));
        }));
        using HttpClient client = factory.CreateClient();
        string email = $"sso-{Guid.NewGuid():N}@example.com";
        using HttpResponseMessage login = await client.PostAsJsonAsync("/auth/login",
            new LoginRequest(OpsDeskApiFactory.AdminEmail, OpsDeskApiFactory.AdminPassword));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        AuthResponse admin = Assert.IsType<AuthResponse>(await login.Content.ReadFromJsonAsync<AuthResponse>());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", admin.AccessToken);
        using HttpResponseMessage invited = await client.PostAsJsonAsync("/admin/invitations", new { email, role = "agent" });
        Assert.Equal(HttpStatusCode.Created, invited.StatusCode);
        string raw = Assert.Single(fixture.Factory.SentInvitations, message => message.RecipientEmail == email).RawToken;
        var identity = new VerifiedExternalIdentity(Issuer, Guid.NewGuid().ToString(), email, true, "SSO", "Agent");
        AuthResponse first;
        using (IServiceScope scope = factory.Services.CreateScope())
        {
            string hash = scope.ServiceProvider.GetRequiredService<IInvitationTokenGenerator>().ComputeHash(raw);
            first = await scope.ServiceProvider.GetRequiredService<IExternalSignInService>().SignInAsync(identity, hash);
        }
        Assert.Equal("Agent", first.Role);
        using (IServiceScope scope = factory.Services.CreateScope())
        {
            AuthResponse again = await scope.ServiceProvider.GetRequiredService<IExternalSignInService>().SignInAsync(identity, null);
            Assert.Equal(first.UserId, again.UserId);
        }
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", first.AccessToken);
        using HttpResponseMessage me = await client.GetAsync("/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        using HttpResponseMessage tickets = await client.GetAsync("/tickets");
        Assert.Equal(HttpStatusCode.OK, tickets.StatusCode);
        client.DefaultRequestHeaders.Authorization = null;
        using HttpResponseMessage passwordLogin = await client.PostAsJsonAsync("/auth/login", new LoginRequest(email, "ValidPass!"));
        Assert.Equal(HttpStatusCode.Unauthorized, passwordLogin.StatusCode);
        using HttpResponseMessage replay = await client.PostAsJsonAsync("/auth/invitations/accept",
            new { token = raw, firstName = "Local", lastName = "User", password = "ValidPass!" });
        Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);
    }
}
```
