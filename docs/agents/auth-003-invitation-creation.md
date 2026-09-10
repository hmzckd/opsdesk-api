# AUTH-003: Davet Olusturma Dilimi

Bu belge 2026-09-06 tarihinde uygulanan ilk dilimi aciklar. Tam dosya icerikleri asagida o tarihin inceleme kopyasi olarak bulunur; sonraki degisikliklerde asil kaynak src/ altindaki dosyalardir.

## Ne Calisiyor?

Admin, POST /admin/invitations adresine email ve role gonderir. Sistem daveti PostgreSQL'e kaydeder, 24 saatlik davet tokenini e-postayla iletir ve 201 cevabinda davet bilgilerini doner. Token API cevabinda bulunmaz. Customer ve Agent davet edilebilir; Admin daveti reddedilir.

Ilk dilimde davet kabul endpoint'i yoktu. Sonraki dilimde eklendi; guncel kabul akisi ve tam kodlar [auth-003-invitation-acceptance.md](auth-003-invitation-acceptance.md) dosyasinda aciklanir. Bu belgedeki kodlar ilk dilimin tarihsel inceleme kopyasidir. AUTH-006 parola sifirlama task'i henuz uygulanmadi.

## Istegin Yolu

`HTTP -> AdminInvitationsController.Create -> InvitationService.CreateAsync -> UserInvitation.Create -> InvitationRepository.AddAsync -> PostgreSQL -> SmtpInvitationEmailSender.SendAsync -> HTTP 201`

Controller HTTP ayrintilarini bilir. Application bu islemlerin sirasini belirler. Domain bir davetin hangi kurallarla olusacagini bilir. Infrastructure PostgreSQL ve SMTP gibi teknolojileri uygular.

## Yeni Klasorler

- `C:/Users/Hamza/Documents/OpsDesk API/src/OpsDesk.Application/Invitations`: Davet ozelligine ait DTO, model, arayuz ve servisleri bir arada tutar.
- `DTOs`: HTTP istegi ve cevabinda kullanilan veri sozlesmeleri.
- `Interfaces`: Application'in ihtiyac duydugu islem sozlesmeleri.
- `Models`: Uygulama ici token ve e-posta verileri.
- `Services`: Davet olusturma is akisi.

Domain/Entities, Infrastructure/Authentication, Infrastructure/Email, Infrastructure/Persistence ve Api/Controllers mevcut klasorlerdir.

## DTO ve Model Dosyalari

`CreateInvitationRequest(string Email, UserRole Role)` API girdisidir. Parola ve davet eden kullanici ID'si istekten alinmaz. Rol sunucuda ayrica kontrol edilir.

`InvitationResponse` API'nin dondugu guvenli bilgidir: Id, Email, Role, InvitedById, CreatedAtUtc ve ExpiresAtUtc. Token veya hash icermez.

`InvitationEmail` servis ile e-posta gonderici arasinda tasinir. Alici, rol, ham token ve son kullanma zamanini icerir. Bu model HTTP cevabi olarak kullanilmaz.

`GeneratedInvitationToken` ham token ile hash'ini birlikte tasir. Ham deger e-postaya, hash veritabanina gider.

`record`, bu gibi veri tasiyan tipleri kisa yazmamizi saglayan C# yapisidir. Parantez icindeki alanlar constructor girdileridir; C# bunlar icin okunabilir ozellikleri de olusturur. `sealed` bu tipten kalitim alinmasini engeller.

## Metotlari Okumak

### AdminInvitationsController.Create

`public async Task<ActionResult<InvitationResponse>> Create(CreateInvitationRequest request, CancellationToken cancellationToken)`

- public: Framework metodu endpoint olarak cagirabilir.
- async: Veritabani ve e-posta tamamlanana kadar await ile bekler; beklerken bir thread'i bloke etmez.
- Task<T>: Islem gelecekte T turunde bir sonuc uretecek.
- ActionResult<InvitationResponse>: HTTP cevabi dondurur; basarida InvitationResponse, kimlik okunamazsa 401 olabilir.
- request: HTTP JSON govdesinden ASP.NET Core tarafindan olusturulur.
- cancellationToken: Istek iptal edilirse alt islemlere haber verir.
- Guid.TryParse: JWT'deki kullanici ID'sinin gercek bir Guid olup olmadigini kontrol eder. out Guid invitedById, parse edilen sonucu bu degiskene yazar.
- Servis sonucu StatusCode(201, response) ile istemciye doner.

Api katmanindadir cunku claim, HTTP durum kodu ve JSON istegi burada ele alinir. Domain bunlari bilmez. Sinif adinin yanindaki `(IInvitationService invitations)` bir primary constructor'dir; DI bu bagimliligi verir. Klasik constructor yaziminin kisa bicimidir.

### InvitationService.CreateAsync

`public async Task<InvitationResponse> CreateAsync(CreateInvitationRequest request, Guid invitedById, CancellationToken cancellationToken = default)`

InvitationResponse ALMAZ; islem bitince onu DONDURUR. request girdiyi, invitedById JWT'den gelen admin kimligini tasir. default, cagiran kod token vermediginde iptal edilmemis varsayilan degeri kullanir.

Girdi kontrolu -> adminin veritabanindaki guncel rolunu okuma -> e-posta dogrulama ve normalizasyon -> mevcut hesap kontrolu -> token uretme -> Domain nesnesini olusturma -> kaydetme -> e-posta gonderme -> response olusturma.

Application katmanindadir cunku bu sira uygulamanin is akisidir. Controller'a yerlestirilseydi HTTP ile is akisi birbirine karisirdi. Servis SQL veya SMTP baglantisi yazmaz; arayuzleri cagirir.

`User?` kullanici bulunamazsa null olabilecegini belirtir. `is not { Role: UserRole.Admin }` kullanicinin mevcut ve Admin olmasini birlikte kontrol eder.

E-posta gonderimi exception firlatirsa catch blogu daveti pasiflestirir ve `throw;` ile hatayi yeniden iletir. Temizlikte CancellationToken.None kullanilir; istemci baglantiyi kapatsa bile etkin ama teslim edilmemis davet kalmasin. Bu bir SMTP outbox/retry sistemi degildir; veritabani ile SMTP arasinda dagitik transaction yoktur.

### UserInvitation.Create

`public static UserInvitation Create(string email, UserRole role, Guid invitedById, string tokenHash, DateTime createdAtUtc)`

static oldugu icin once bir UserInvitation nesnesi yaratmak gerekmez; `UserInvitation.Create(...)` diye cagrilir. Donus tipi UserInvitation'dir.

email normalize edilmis alici, role hedef yetki, invitedById davet eden admin, tokenHash saklanacak hash ve createdAtUtc UTC olusturma zamanidir. Girdiler kontrol edilir; 24 saatlik son kullanma zamani hesaplanir.

Domain katmanindadir cunku desteklenen roller ve sure davetin is kurallaridir. Veritabanina yazmaz veya e-posta gondermez. private constructor, normal uygulama kodunun kurallari atlayarak bos nesne olusturmasini engeller; EF Core nesneyi veritabanindan okurken kullanabilir.

### InvitationRepository.AddAsync

`public async Task AddAsync(UserInvitation invitation, CancellationToken cancellationToken = default)`

Task, sonuc nesnesi dondurmeyen asenkron islem demektir. invitation Domain'de olusturulan kayittir.

Bir transaction acilir. Ayni adresin suresi dolmus etkin davetleri pasiflestirilir. Yeni kayit eklenip kaydedilir ve transaction commit edilir. Commit, islemleri kalici hale getirir. Hata halinde transaction dispose edilirken geri alinir.

PostgreSQL'in benzersiz indeks ihlali yalnizca ilgili davet indeksi icin 409'a cevrilir. Diger veritabani hatalari gizlenmez. Cunku iki istek ayni anda geldi diye sadece onceden 'var mi?' sorgusu yapmak yeterli olmaz.

Infrastructure katmanindadir; ExecuteUpdateAsync, SaveChangesAsync ve PostgreSQL hata kodlari teknoloji ayrintilaridir.

### InvitationRepository.RevokeAsync

`public async Task RevokeAsync(Guid invitationId, DateTime revokedAtUtc, CancellationToken cancellationToken = default)`

invitationId hangi davetin pasiflestirilecegini, revokedAtUtc ne zaman oldugunu belirtir. ExecuteUpdateAsync tek SQL UPDATE ile RevokedAtUtc degerini yazar. Kaydi silmez. Gonderim hatasindan sonra tekrar davet acilabilmesini saglar.

### UserInvitationConfiguration.Configure

`public void Configure(EntityTypeBuilder<UserInvitation> builder)`

void sonuc donmedigini belirtir. builder, EF Core'un bu entity'nin tablo karsiligini tarif ettigimiz nesnesidir. Tablo adi, sutunlar, uzunluklar, rol/sure kontrolleri ve foreign key burada belirtilir.

`HasFilter("revoked_at_utc IS NULL")` indeksi sadece pasiflestirilmemis kayitlara uygular. Boylece gecmis kayitlar saklanirken bir adres icin tek bekleyen davet bulunur. Foreign key, davet eden kullanicinin users tablosunda bulunmasini zorunlu tutar.

### InvitationTokenGenerator.GenerateToken

`public GeneratedInvitationToken GenerateToken()`

Mevcut kriptografik ureticiden rastgele deger alir, basina inv_ ekler ve tam degerin hash'ini hesaplar. Davet hash'leri ayri tabloda saklanir; dogrulama endpoint'i bu tabloya bakmaz. Bu ayrim, bir davet tokeninin e-posta dogrulama tokeni olarak kullanilmasini engeller. Kabul dilimi kendi amac kontrolunu ayrica uygulayacak.

Infrastructure katmanindadir cunku rastgele deger uretimi ve hash algoritmasi teknik uygulamadir. Mevcut ureticinin EmailVerification adini koruduk; bu isimlendirme sinirlamasi gelecekte ortak bir token ureticiye geciste ele alinabilir.

### SmtpInvitationEmailSender.SendAsync

`public async Task SendAsync(InvitationEmail email, CancellationToken cancellationToken = default)`

email, Application servisinin urettigi e-posta modelidir. MimeMessage olusturur, gonderici ve aliciyi ekler, davet metnini hazirlar ve ortak SMTP yardimcisini cagirir.

`IOptions<EmailSettings>`, .NET'in ayarlari tipli bir nesne olarak verme yoludur. `settings.Value` ile Host, Port, FromAddress gibi ayarlara ulasiriz. Parantezdeki <EmailSettings> bu nesnenin hangi ayar tipini tasidigini belirtir.

### SmtpEmailTransport.SendAsync

`internal static async Task SendAsync(EmailSettings settings, MimeMessage message, CancellationToken cancellationToken)`

internal, sadece Infrastructure projesinin icinden kullanilabilecegini belirtir. settings baglanti ayarlari, message hazir e-posta nesnesidir. SMTP'ye baglanir, mesaji gonderir ve finally blogunda baglantiyi kapatir. finally, gonderim basarili veya hatali olsa da calisir.

Mevcut SmtpEmailVerificationEmailSender.SendAsync de ayni yardimciyi kullanir. E-posta icerikleri ayri, baglanti davranisi ortaktir. Gercek Mailpit testleri iki gonderimi de dogrular.

## Arayuzler ve DI

IInvitationService controller'in cagiracagi is akisini; IInvitationRepository kalici kayit islemlerini; IInvitationTokenGenerator token uretimini; IInvitationEmailSender teslimati tarif eder.

Program.cs servis arayuzunu InvitationService'e baglar. Infrastructure/DependencyInjection.cs repository, token uretici ve SMTP gondericiyi kaydeder. Scoped servis request basina olusur; Singleton gonderici/ureticinin paylasilan durumu yoktur ve her gonderimde yeni SMTP client acilir.

OpsDeskDbContext.UserInvitations, EF Core'un bu entity'ye sorgu ve yazma erisimidir. Migration dosyasinin Up metodu user_invitations tablosunu, indeksleri ve foreign key'i olusturur; Down metodu geri alma icin tabloyu siler. Migration EF CLI ile olusturuldu; gelistirme veritabanina bu turda manuel database update uygulanmadi. Test fixture migration'i gecici test veritabanina uygular.

## Testler

InvitationCreationIntegrationTests API uzerinden admin olusturma, tokenin response'ta bulunmamasi, rol ve adres kontrolu, eszamanli davet, suresi dolan davet ve SMTP hatasindan sonra tekrar denemeyi kapsar. Kabul islemi henuz test edilmez cunku bu dilimde uygulanmadi.

LoginAsAdminAsync gercek login cevabindan JWT alir. InvitationTestClock.GetUtcNow test saatini kontrollu ilerletir; 24 saat beklemek gerekmez. FailingInvitationSender.SendAsync bilerek SMTP hatasi uretir; endpoint'in hata sonrasi davranisini dogrular. RecordingInvitationSender.SendAsync gonderilen mesajlari thread-safe ConcurrentQueue icinde toplar.

HTTP testlerinde e-posta sinirinda kayit yapan test gondericisi kullanilir. SmtpEmailVerificationEmailSenderTests icindeki yeni davet testi ise gercek Mailpit konteynerine SMTP uzerinden gonderim yapar.

Ilk olusturma testi uygulama yazilmadan once beklenen 404 nedeniyle basarisiz oldu; uygulamadan sonra gecti. Diger kenar-durum testleri bu davranis uzerine regresyon kontrolu olarak eklendi. Her birinin ayri bir red-green dongusu yasadigi iddia edilmiyor.

Dogrulama: 258 test basarili, build 0 uyari/0 hata, Slopwatch 0 bulgu.

## Tam Yeni Dosyalar

### src/OpsDesk.Domain/Entities/UserInvitation.cs

Tam yol: C:/Users/Hamza/Documents/OpsDesk API/src/OpsDesk.Domain/Entities/UserInvitation.cs

```csharp
using OpsDesk.Domain.Enums;

namespace OpsDesk.Domain.Entities;

public sealed class UserInvitation
{
    public Guid Id { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public UserRole Role { get; private set; }
    public Guid InvitedById { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }

    private UserInvitation() { }

    // Creates a pending invitation with the role and lifetime fixed by server rules.
    public static UserInvitation Create(
        string email, UserRole role, Guid invitedById,
        string tokenHash, DateTime createdAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);
        if (email.Length > 254 || tokenHash.Length != 64)
        {
            throw new ArgumentException("Invitation email or token hash is invalid.");
        }

        if (role is not (UserRole.Customer or UserRole.Agent))
        {
            throw new ArgumentException("Only Customer and Agent invitations are allowed.", nameof(role));
        }

        if (invitedById == Guid.Empty || createdAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Invitation requires an inviter and a UTC creation time.");
        }

        return new UserInvitation
        {
            Id = Guid.NewGuid(),
            Email = email,
            Role = role,
            InvitedById = invitedById,
            TokenHash = tokenHash,
            CreatedAtUtc = createdAtUtc,
            ExpiresAtUtc = createdAtUtc.AddHours(24)
        };
    }
}
```

### src/OpsDesk.Application/Invitations/DTOs/CreateInvitationRequest.cs

Tam yol: C:/Users/Hamza/Documents/OpsDesk API/src/OpsDesk.Application/Invitations/DTOs/CreateInvitationRequest.cs

```csharp
using OpsDesk.Domain.Enums;

namespace OpsDesk.Application.Invitations.DTOs;

public sealed record CreateInvitationRequest(string Email, UserRole Role);
```

### src/OpsDesk.Application/Invitations/DTOs/InvitationResponse.cs

Tam yol: C:/Users/Hamza/Documents/OpsDesk API/src/OpsDesk.Application/Invitations/DTOs/InvitationResponse.cs

```csharp
using OpsDesk.Domain.Enums;

namespace OpsDesk.Application.Invitations.DTOs;

public sealed record InvitationResponse(
    Guid Id, string Email, UserRole Role, Guid InvitedById,
    DateTime CreatedAtUtc, DateTime ExpiresAtUtc);
```

### src/OpsDesk.Application/Invitations/Models/InvitationEmail.cs

Tam yol: C:/Users/Hamza/Documents/OpsDesk API/src/OpsDesk.Application/Invitations/Models/InvitationEmail.cs

```csharp
using OpsDesk.Domain.Enums;

namespace OpsDesk.Application.Invitations.Models;

public sealed record InvitationEmail(
    string RecipientEmail, UserRole Role, string RawToken, DateTime ExpiresAtUtc);
```

### src/OpsDesk.Application/Invitations/Models/GeneratedInvitationToken.cs

Tam yol: C:/Users/Hamza/Documents/OpsDesk API/src/OpsDesk.Application/Invitations/Models/GeneratedInvitationToken.cs

```csharp
namespace OpsDesk.Application.Invitations.Models;

public sealed record GeneratedInvitationToken(string RawToken, string TokenHash);
```

### src/OpsDesk.Application/Invitations/Interfaces/IInvitationService.cs

Tam yol: C:/Users/Hamza/Documents/OpsDesk API/src/OpsDesk.Application/Invitations/Interfaces/IInvitationService.cs

```csharp
using OpsDesk.Application.Invitations.DTOs;

namespace OpsDesk.Application.Invitations.Interfaces;

public interface IInvitationService
{
    // Creates one invitation for the authenticated inviter and sends its token by email.
    Task<InvitationResponse> CreateAsync(
        CreateInvitationRequest request, Guid invitedById,
        CancellationToken cancellationToken = default);
}
```

### src/OpsDesk.Application/Invitations/Interfaces/IInvitationRepository.cs

Tam yol: C:/Users/Hamza/Documents/OpsDesk API/src/OpsDesk.Application/Invitations/Interfaces/IInvitationRepository.cs

```csharp
using OpsDesk.Domain.Entities;

namespace OpsDesk.Application.Invitations.Interfaces;

public interface IInvitationRepository
{
    // Atomically retires expired invitations and persists a new pending invitation.
    Task AddAsync(UserInvitation invitation, CancellationToken cancellationToken = default);

    // Retires an undelivered invitation so the admin can retry safely.
    Task RevokeAsync(Guid invitationId, DateTime revokedAtUtc,
        CancellationToken cancellationToken = default);
}
```

### src/OpsDesk.Application/Invitations/Interfaces/IInvitationTokenGenerator.cs

Tam yol: C:/Users/Hamza/Documents/OpsDesk API/src/OpsDesk.Application/Invitations/Interfaces/IInvitationTokenGenerator.cs

```csharp
using OpsDesk.Application.Invitations.Models;

namespace OpsDesk.Application.Invitations.Interfaces;

public interface IInvitationTokenGenerator
{
    // Generates a purpose-specific token and its storage hash.
    GeneratedInvitationToken GenerateToken();
}
```

### src/OpsDesk.Application/Invitations/Interfaces/IInvitationEmailSender.cs

Tam yol: C:/Users/Hamza/Documents/OpsDesk API/src/OpsDesk.Application/Invitations/Interfaces/IInvitationEmailSender.cs

```csharp
using OpsDesk.Application.Invitations.Models;

namespace OpsDesk.Application.Invitations.Interfaces;

public interface IInvitationEmailSender
{
    // Delivers the raw invitation token to its intended recipient.
    Task SendAsync(InvitationEmail email, CancellationToken cancellationToken = default);
}
```

### src/OpsDesk.Application/Invitations/Services/InvitationService.cs

Tam yol: C:/Users/Hamza/Documents/OpsDesk API/src/OpsDesk.Application/Invitations/Services/InvitationService.cs

```csharp
using OpsDesk.Application.Auth.Interfaces;
using OpsDesk.Application.Auth.Services;
using OpsDesk.Application.Common.Exceptions;
using OpsDesk.Application.Invitations.DTOs;
using OpsDesk.Application.Invitations.Interfaces;
using OpsDesk.Application.Invitations.Models;
using OpsDesk.Domain.Entities;
using OpsDesk.Domain.Enums;

namespace OpsDesk.Application.Invitations.Services;

public sealed class InvitationService(
    IUserRepository users,
    IInvitationRepository invitations,
    IInvitationTokenGenerator tokens,
    IInvitationEmailSender emailSender,
    IEmailValidator emailValidator,
    TimeProvider timeProvider) : IInvitationService
{
    // Validates the actor and recipient, stores a pending invitation, then delivers it.
    public async Task<InvitationResponse> CreateAsync(
        CreateInvitationRequest request, Guid invitedById,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        User? inviter = await users.GetByIdAsync(invitedById, cancellationToken);
        if (inviter is not { Role: UserRole.Admin })
        {
            throw new ForbiddenException("Only an Admin can invite users.");
        }

        emailValidator.Validate(request.Email);
        string email = UserInputNormalizer.NormalizeEmail(request.Email);
        if (await users.ExistsByEmailAsync(email, cancellationToken))
        {
            throw new ConflictException("An account with this email already exists.");
        }

        GeneratedInvitationToken token = tokens.GenerateToken();
        UserInvitation invitation = UserInvitation.Create(
            email, request.Role, invitedById, token.TokenHash,
            timeProvider.GetUtcNow().UtcDateTime);
        await invitations.AddAsync(invitation, cancellationToken);

        try
        {
            await emailSender.SendAsync(new InvitationEmail(
                email, invitation.Role, token.RawToken, invitation.ExpiresAtUtc),
                cancellationToken);
        }
        catch
        {
            // A failed delivery must not leave an active invitation blocking a retry.
            await invitations.RevokeAsync(invitation.Id,
                timeProvider.GetUtcNow().UtcDateTime, CancellationToken.None);
            throw;
        }

        return new InvitationResponse(invitation.Id, invitation.Email,
            invitation.Role, invitation.InvitedById,
            invitation.CreatedAtUtc, invitation.ExpiresAtUtc);
    }
}
```

### src/OpsDesk.Infrastructure/Authentication/InvitationTokenGenerator.cs

Tam yol: C:/Users/Hamza/Documents/OpsDesk API/src/OpsDesk.Infrastructure/Authentication/InvitationTokenGenerator.cs

```csharp
using OpsDesk.Application.Auth.Interfaces;
using OpsDesk.Application.Invitations.Interfaces;
using OpsDesk.Application.Invitations.Models;

namespace OpsDesk.Infrastructure.Authentication;

public sealed class InvitationTokenGenerator(IEmailVerificationTokenGenerator generator)
    : IInvitationTokenGenerator
{
    // Reuses the cryptographic generator while separating invitation tokens by purpose.
    public GeneratedInvitationToken GenerateToken()
    {
        string rawToken = "inv_" + generator.GenerateToken().RawToken;
        return new GeneratedInvitationToken(rawToken, generator.ComputeHash(rawToken));
    }
}
```

### src/OpsDesk.Infrastructure/Persistence/Configurations/UserInvitationConfiguration.cs

Tam yol: C:/Users/Hamza/Documents/OpsDesk API/src/OpsDesk.Infrastructure/Persistence/Configurations/UserInvitationConfiguration.cs

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpsDesk.Domain.Entities;

namespace OpsDesk.Infrastructure.Persistence.Configurations;

public sealed class UserInvitationConfiguration : IEntityTypeConfiguration<UserInvitation>
{
    // Stores invitation history and permits only one non-revoked invitation per address.
    public void Configure(EntityTypeBuilder<UserInvitation> builder)
    {
        builder.ToTable("user_invitations", table =>
        {
            table.HasCheckConstraint("ck_user_invitations_expiry", "expires_at_utc > created_at_utc");
            table.HasCheckConstraint("ck_user_invitations_role", "role IN ('Customer', 'Agent')");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.Email).HasColumnName("email").HasMaxLength(254).IsRequired();
        builder.Property(x => x.Role).HasColumnName("role").HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.InvitedById).HasColumnName("invited_by_id");
        builder.Property(x => x.TokenHash).HasColumnName("token_hash").HasMaxLength(64).IsRequired();
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(x => x.ExpiresAtUtc).HasColumnName("expires_at_utc");
        builder.Property(x => x.RevokedAtUtc).HasColumnName("revoked_at_utc");
        builder.HasIndex(x => x.Email).IsUnique()
            .HasFilter("revoked_at_utc IS NULL")
            .HasDatabaseName("ux_user_invitations_pending_email");
        builder.HasIndex(x => x.TokenHash).IsUnique();
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.InvitedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
```

### src/OpsDesk.Infrastructure/Persistence/Repositories/InvitationRepository.cs

Tam yol: C:/Users/Hamza/Documents/OpsDesk API/src/OpsDesk.Infrastructure/Persistence/Repositories/InvitationRepository.cs

```csharp
using Microsoft.EntityFrameworkCore;
using Npgsql;
using OpsDesk.Application.Common.Exceptions;
using OpsDesk.Application.Invitations.Interfaces;
using OpsDesk.Domain.Entities;

namespace OpsDesk.Infrastructure.Persistence.Repositories;

public sealed class InvitationRepository(OpsDeskDbContext database) : IInvitationRepository
{
    // Retires expired records and saves the replacement within one transaction.
    public async Task AddAsync(UserInvitation invitation, CancellationToken cancellationToken = default)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        await database.UserInvitations
            .Where(x => x.Email == invitation.Email && x.RevokedAtUtc == null
                && x.ExpiresAtUtc <= invitation.CreatedAtUtc)
            .ExecuteUpdateAsync(setters => setters.SetProperty(
                x => x.RevokedAtUtc, invitation.CreatedAtUtc), cancellationToken);

        database.UserInvitations.Add(invitation);
        try
        {
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: "ux_user_invitations_pending_email"
            })
        {
            throw new ConflictException("An active invitation for this email already exists.");
        }
    }

    // Keeps an unsuccessful invitation as history while releasing its pending-email slot.
    public async Task RevokeAsync(Guid invitationId, DateTime revokedAtUtc,
        CancellationToken cancellationToken = default)
    {
        await database.UserInvitations.Where(x => x.Id == invitationId && x.RevokedAtUtc == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(
                x => x.RevokedAtUtc, revokedAtUtc), cancellationToken);
    }
}
```

### src/OpsDesk.Infrastructure/Email/SmtpEmailTransport.cs

Tam yol: C:/Users/Hamza/Documents/OpsDesk API/src/OpsDesk.Infrastructure/Email/SmtpEmailTransport.cs

```csharp
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace OpsDesk.Infrastructure.Email;

internal static class SmtpEmailTransport
{
    // Shares SMTP connection and cleanup behavior between verification and invitation emails.
    internal static async Task SendAsync(
        EmailSettings settings, MimeMessage message, CancellationToken cancellationToken)
    {
        using var client = new SmtpClient();
        await client.ConnectAsync(settings.Host, settings.Port,
            settings.UseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.None,
            cancellationToken);
        try
        {
            await client.SendAsync(message, cancellationToken);
        }
        finally
        {
            if (client.IsConnected)
            {
                await client.DisconnectAsync(true, CancellationToken.None);
            }
        }
    }
}
```

### src/OpsDesk.Infrastructure/Email/SmtpInvitationEmailSender.cs

Tam yol: C:/Users/Hamza/Documents/OpsDesk API/src/OpsDesk.Infrastructure/Email/SmtpInvitationEmailSender.cs

```csharp
using Microsoft.Extensions.Options;
using MimeKit;
using OpsDesk.Application.Invitations.Interfaces;
using OpsDesk.Application.Invitations.Models;

namespace OpsDesk.Infrastructure.Email;

public sealed class SmtpInvitationEmailSender(IOptions<EmailSettings> settings)
    : IInvitationEmailSender
{
    // Sends an invitation token using the shared SMTP connection settings.
    public async Task SendAsync(InvitationEmail email, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(email);
        using var message = new MimeMessage();
        message.From.Add(new MailboxAddress(settings.Value.FromName, settings.Value.FromAddress));
        message.To.Add(new MailboxAddress(string.Empty, email.RecipientEmail));
        message.Subject = "You are invited to OpsDesk";
        message.Body = new TextPart("plain")
        {
            Text = $"You have been invited to OpsDesk as {email.Role}.\n\n" +
                $"Invitation token: {email.RawToken}\n\n" +
                $"Expires at {email.ExpiresAtUtc:O} (UTC).\n" +
                "Use this token to accept your invitation. Do not share it."
        };
        await SmtpEmailTransport.SendAsync(settings.Value, message, cancellationToken);
    }
}
```

### src/OpsDesk.Api/Controllers/AdminInvitationsController.cs

Tam yol: C:/Users/Hamza/Documents/OpsDesk API/src/OpsDesk.Api/Controllers/AdminInvitationsController.cs

```csharp
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpsDesk.Application.Authorization;
using OpsDesk.Application.Invitations.DTOs;
using OpsDesk.Application.Invitations.Interfaces;

namespace OpsDesk.Api.Controllers;

[ApiController]
[Route("admin/invitations")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public sealed class AdminInvitationsController(IInvitationService invitations) : ControllerBase
{
    // Takes the inviter identity from the validated JWT, never from user-supplied JSON.
    [HttpPost]
    [ProducesResponseType<InvitationResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<InvitationResponse>> Create(
        CreateInvitationRequest request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out Guid invitedById))
        {
            return Unauthorized();
        }

        InvitationResponse response = await invitations.CreateAsync(request, invitedById, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, response);
    }
}
```
