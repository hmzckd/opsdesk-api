# AUTH-003: Davet Kabul Etme

Bu belge 2026-09-06 tarihindeki kabul dilimini aciklar. Asagidaki tam kodlar inceleme kopyasidir; guncel kaynak src/ ve tests/ dosyalaridir.

## Task'in Amaci

Admin'in davet ettigi kisinin e-postadaki tek kullanimlik token ile dogrulanmis bir Customer veya Agent hesabi acabilmesini saglamak.

## Kullanici Akisi

1. Admin POST /admin/invitations ile email ve role belirler.
2. Davet e-postasi 24 saat gecerli bir token tasir. Gelistirmede bu posta Mailpit'e gider; gercek dis posta teslimi iddia edilmez.
3. Alici POST /auth/invitations/accept ile token, firstName, lastName ve password gonderir.
4. Sunucu e-posta ve rolu istegin govdesinden degil, kayitli davetten alir.
5. Hesap olusur ve davet tek transaction icinde tuketilir. Hesap e-postasi dogrulanmis kabul edilir: davet tokenine sahip olmak, e-postaya erisim kaniti olarak kullanilir. Bu nedenle token gizli tutulmalidir.
6. Cevap 201 ve UserId, Email, Role, EmailVerifiedAtUtc bilgileridir. JWT veya parola donmez.
7. Kullanici POST /auth/login ile giris yapar; burada JWT alir.

Bu dilimde tarayicida davet kabul formu yoktur. Swagger/API ile kullanilir. Davet tokeni Bearer access token degildir ve Authorize kutusuna yazilmaz.

## Istegin Katmanlardan Gecisi

HTTP -> InvitationsController.Accept -> InvitationService.AcceptAsync -> InvitationTokenGenerator.ComputeHash -> InvitationRepository.GetByTokenHashAsync -> UserInvitation.CanAcceptAt -> mevcut validator/hasher -> User.MarkEmailVerified -> InvitationRepository.TryAcceptAsync -> PostgreSQL -> AcceptedInvitationResponse -> HTTP 201

- Api: HTTP'yi bilir; hangi HTTP kodunun donecegini belirler.
- Application: is sirasini belirler; dogrulama, parola hashleme ve kayit islemlerini koordine eder.
- Domain: davetin hangi kosullarda kabul edilebilecegini bilir; HTTP veya EF Core bilmez.
- Infrastructure: hash teknolojisini ve PostgreSQL transaction/SQL ayrintilarini uygular.
- Tests: bu davranislari gercek HTTP istekleri ve test PostgreSQL'iyle denetler.

## DTO Neden Ayri?

AcceptInvitationRequest yalnizca kullanicinin gonderebilecegi verileri tasir. User entity'sini dogrudan almak, Role, EmailVerifiedAtUtc gibi alanlarin istemci tarafindan degistirilmeye calisilmasina kapi acardi.

AcceptedInvitationResponse ise kullaniciya geri verecegimiz alanlari sinirlar. PasswordHash ve TokenHash cevaba dahil edilmez.

record: veri tasimak icin kullanilan C# turudur; pozisyonel record yazimi constructor ve property'leri uretir.
sealed: bu siniftan/record'dan baska bir sinif turetilmesini engeller.
DTO'daki string Token bir girdi; response'daki Guid UserId bir ciktidir. Ayni sey degiller.

## Metotlari Okumak

### InvitationsController.Accept

Dosya: C:/Users/Hamza/Documents/OpsDesk API/src/OpsDesk.Api/Controllers/InvitationsController.cs

public async Task<ActionResult<AcceptedInvitationResponse>> Accept(AcceptInvitationRequest request, CancellationToken cancellationToken)

- public: ASP.NET Core tarafindan cagrilabilen acik metot.
- async: govdede await ile tamamlanmasi beklenecek isler var.
- Task<T>: ileride tamamlanacak islem, bitince T tipinde sonuc saglar. Ayrica yeni bir thread acildigi anlamina gelmez.
- ActionResult<T>: HTTP sonucu ile T tipindeki cevap govdesini birlestirir. Burada T, AcceptedInvitationResponse.
- request: JSON govdesinden olusturulan AcceptInvitationRequest. Kullanici girdisidir.
- cancellationToken: istemci baglantiyi kestiginde islemin iptal edilmesini bildirebilir. Servis ve DB cagrilarina iletilir.
- Govde: servisi await eder ve 201 doner. SQL veya parola hashleme yapmaz.
- AllowAnonymous: alicinin daha hesabi olmadigi icin JWT istemez. Islem davet tokeniyle sinirlandirilir.

### InvitationService.AcceptAsync

Dosya: C:/Users/Hamza/Documents/OpsDesk API/src/OpsDesk.Application/Invitations/Services/InvitationService.cs

public async Task<AcceptedInvitationResponse> AcceptAsync(AcceptInvitationRequest request, CancellationToken cancellationToken = default)

Metot AcceptedInvitationResponse ALMAZ; tamamlandiginda bu tipte veri DONDURUR. Aldigi sey AcceptInvitationRequest'tir.
default, cagirici iptal bilgisi vermezse varsayilan tokenin kullanilmasidir; HTTP controller gercek request tokenini gonderir.

Govde:
1. request ve tokeni kontrol eder; tokenin hash'ini hesaplatir.
2. Kayitli daveti getirir, kullanilabilir olup olmadigini sorar.
3. Mevcut parola validatorunu kullanir; ad ve soyadi trim edip dogrular.
4. Parolayi hashler. Duz parola User'a veya veritabanina yazilmaz.
5. Hashleme zaman alabileceginden UTC zamani yeniden alip davetin gecerliligini tekrar kontrol eder.
6. Davetteki email/role ile User olusturur ve e-postasini dogrulanmis isaretler.
7. TryAcceptAsync ile hesabi ve davet tuketimini birlikte kaydeder.
8. Basariliysa guvenli cevap DTO'sunu doner; davet gecersizse ArgumentException firlatir.

Neden Application? Bu bir is akisidir. Controller'a koysaydik HTTP katmani is kurallarini yonetirdi; Infrastructure'a koysaydik uygulama akisini PostgreSQL'e baglamis olurduk.

Constructor'a eklenen IPasswordValidator ve IPasswordHasher mevcut DI kayitlarindan gelir. Birincisi parola kurallarini denetler, ikincisi hash uretir. Yeni paket gerekmedi.

### UserInvitation.CanAcceptAt

Dosya: C:/Users/Hamza/Documents/OpsDesk API/src/OpsDesk.Domain/Entities/UserInvitation.cs

public bool CanAcceptAt(DateTime acceptedAtUtc)

bool: true veya false doner. Parametre kontrol edilmek istenen UTC zamandir; zamanin kaynagi Application'in TimeProvider'idir.
UTC degilse hata verir. Davet iptal edilmemis, kabul edilmemis, baslangic zamani gelmis ve son kullanma zamani gecmemisse true doner.
Tam son kullanma aninda artik gecersizdir. Kayit degistirmez, DB'ye gitmez.
Neden Domain? Bunlar davetin is kurallaridir; SQL ve HTTP ayrintisi degildir.

Eklenen DateTime? AcceptedAtUtc ve Guid? AcceptedUserId alanlarindaki ? degerin null olabilecegini belirtir: henuz kabul edilmemis davette bu bilgiler yoktur.

### InvitationTokenGenerator.ComputeHash

Dosya: C:/Users/Hamza/Documents/OpsDesk API/src/OpsDesk.Infrastructure/Authentication/InvitationTokenGenerator.cs

public string ComputeHash(string rawToken)

rawToken e-postadan gelen gizli davet tokenidir. Donen string SHA-256 hash'idir; ham tokeni veritabaninda aramak yerine hash'ini arariz.
Bosluk, uzunluk, inv_ on eki ve URL-safe karakterleri kontrol eder. Bicim hataliysa generic bir gecersiz-davet hatasi verir.
GenerateToken ayni ComputeHash metodunu kullanacak sekilde guncellendi: uretim ve kontrol ayni hash kuralini kullanir.
Neden Infrastructure? Kriptografi somut teknoloji ayrintisidir. Application sadece IInvitationTokenGenerator sozlesmesini bilir.
Mevcut email verification token generatorunun kriptografik cekirdegi yeniden kullanilir; kabul endpoint'i yalnizca davet tablosunda arar. Bir email verification tokeni davet yerine kullanilamaz.

### InvitationRepository.GetByTokenHashAsync

Dosya: C:/Users/Hamza/Documents/OpsDesk API/src/OpsDesk.Infrastructure/Persistence/Repositories/InvitationRepository.cs

public Task<UserInvitation?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default)

Task<UserInvitation?>: islem bittiginde bir davet veya null doner. tokenHash servis tarafindan hesaplanir.
AsNoTracking: yalnizca okunacak kaydi EF'in degisiklik takip listesine eklemez.
SingleOrDefaultAsync: eslesen kaydi veya null doner. Unique index ayni hash icin iki davet bulunmasini engeller.
Bu metotta async anahtar kelimesi yoktur: EF'in urettigi Task dogrudan geri verilir; cagirici await eder.
Neden Infrastructure? EF Core sorgusu burada bulunur; arayuzu Application'da tutulur.

### InvitationRepository.TryAcceptAsync

public async Task<bool> TryAcceptAsync(Guid invitationId, User user, DateTime acceptedAtUtc, CancellationToken cancellationToken = default)

- invitationId: okunmus davetin kimligi, JSON'dan gelmez.
- user: Application'in dogrulanmis girdiler ve davetteki kimlikle olusturdugu yeni hesap.
- acceptedAtUtc: Application'in son UTC kontrol zamani.
- cancellationToken: request iptal bilgisini DB'ye tasir.
- Task<bool>: true = davet tuketildi ve hesap kaydedildi; false = davet artik kullanilamaz. DB hatalari false gibi gizlenmez.

Govde:
1. BeginTransactionAsync ile transaction baslar.
2. Davet aktifse, email/role uyuyorsa ve gecerlilik araligindaysa accepted_at_utc kosullu SQL UPDATE ile yazilir.
3. Etkilenen satir sayisi sifirsa false donulur. await using transaction'i kapatir ve commit edilmedigi icin geri alir.
4. Yeni User eklenir ve SaveChangesAsync ile SQL'e yazilir.
5. accepted_user_id yeni hesaba baglanir.
6. CommitAsync tum islemleri kalici hale getirir.
7. Email unique constraint'e takilirsa ConflictException firlatilir. Commit olmadigindan davetin kabul tarihi de geri alinir.

ExecuteUpdateAsync: entity'yi bellekte degistirip SaveChanges beklemek yerine dogrudan SQL UPDATE calistirir.
Satir kilidi: iki istek ayni daveti tuketmeye calisirsa ilk guncelleme digerini bekletir; ilk commit sonrasinda ikinci istek aktif-davet kosulunu artik saglayamaz.
Bu nedenle sadece once okuyup if kontrolu yapmak yeterli degildir; kosul yazma aninda da DB'de denetlenir.

accepted_user_id neden ikinci UPDATE ile yaziliyor? Kullanici henuz DB'de yokken foreign key ona baglanamaz. Once hesap eklenir, sonra baglanir; ikisi ayni transaction icindedir. Disaridaki normal bir okuma yarim kabul kaydini gormez.

Neden Infrastructure? Transaction, SQL, PostgreSQL hata kodlari ve satir kilidi burada uygulanir. Islem sozlesmesi Application'dadir.

### AddAsync ve RevokeAsync Degisikligi

Ayni repository'deki mevcut iki metot artik AcceptedAtUtc == null kosulunu da kullanir.
AddAsync expired kayitlari emekliye ayirirken kabul edilmis davetleri degistirmez.
RevokeAsync gec kalmis bir SMTP hatasi yuzunden zaten kabul edilmis daveti iptal etmez.
Parametreler onceki dilimle aynidir; tam aciklama auth-003-invitation-creation.md dosyasindadir.

### UserInvitationConfiguration.Configure ve Migration

Dosya: C:/Users/Hamza/Documents/OpsDesk API/src/OpsDesk.Infrastructure/Persistence/Configurations/UserInvitationConfiguration.cs

public void Configure(EntityTypeBuilder<UserInvitation> builder)

void: sonuc degeri donmez. EF Core'un verdigi builder uzerinden tablo/kolon kurallari tanimlanir.
Iki nullable kolon ve AcceptedUserId -> users.id foreign key eklendi.
Pending-email unique index artik yalnizca iptal edilmemis VE kabul edilmemis davetlere uygulanir.
Foreign key, davetteki kabul edilen kullanici kimliginin gercek bir hesabi gostermesini garanti eder.

Migration: C:/Users/Hamza/Documents/OpsDesk API/src/OpsDesk.Infrastructure/Persistence/Migrations/20260906122925_AddInvitationAcceptance.cs
EF CLI tarafindan uretildi. Up semayi ileri tasir, Down bu sema adimini geri alir. Down kabul metadata kolonlarini kaldirdigi icin veri kaybi yaratabilir; calistirilmadi.
protected override: EF'in temel Migration sinifindaki sanal metodu bu migration'a ozel doldururuz. Designer ve Snapshot EF'in model kayitlaridir; elle yazilmadi.

Migration entegrasyon testlerinin gecici veritabanina uygulandi. Kalici gelistirme veritabanina bu turda database update uygulanmadi.

## Testleri Nasil Okumali?

Dosya: C:/Users/Hamza/Documents/OpsDesk API/tests/OpsDesk.Tests/Integration/InvitationAcceptanceIntegrationTests.cs

public async Task testMetodu(): xUnit'in calistirdigi, await iceren ve veri sonucu donmeyen test. Basarisizlik Assert veya exception ile bildirilir.
Fact: tek senaryo. Theory ve InlineData: ayni senaryoyu verilen farkli girdilerle tekrar calistirir.
Test sinifina gelen OpsDeskApiFixture ortak test PostgreSQL'ini ve API factory'sini saglar; gercek gelistirme DB'sini kullanmaz.
CreateClient test sunucusuna HTTP istegi atan istemciyi verir. Uretim controller/service/repository akisi calisir; bu testte e-postalar kayit alan test gondericisine gider.

- Accept_should_create_verified_account_and_reject_replay(role, loginRole): Agent ve Customer icin 201, tekrar 400, login ve dogrulanmis ticket erisimini denetler. Iki string parametre InlineData'dan gelir; JSON enum ve auth response string yazimlari farklidir.
- Invalid_input_should_allow_corrected_retry(firstName, lastName, password): uc gecersiz girdi kombinasyonunu dener, sonra duzeltir. Ekstra email/role alanlarinin daveti degistirmedigini de kontrol eder.
- Concurrent_acceptance_should_have_one_winner(): Task.WhenAll ile iki HTTP istegi baslatir; biri 201 digeri 400 olmali. Buradaki Task dizisi paralel istek sonuclarini toplar; uretim servisinde yeni thread acildigi anlamina gelmez.
- Existing_account_should_remain_unchanged_and_acceptance_should_roll_back(): davetten sonra self-register olur; iki kabul denemesi de 409 olmali. Ikinci cevap 400 olsaydi davet yanlislikla tuketilmis olabilirdi. Eski parola/rol/ad ve dogrulanmamis durum korunur.
- Verification_and_unknown_tokens_should_not_accept_invitation(): baska amacli, bilinmeyen veya bozuk tokenlerle 400 bekler. Asil verification tokeni normal confirm endpoint'inde hala calisir.
- Expired_and_replaced_tokens_should_be_rejected_but_new_token_should_work(): saati 24 saat ileri alir; eski davet reddedilir, yenisi kabul edilir. Gercekten 24 saat beklemez.
- private InviteAsync(client, email, role = "agent"): test hazirlik yardimcisi. Admin login -> invitation HTTP akisiyla tokeni test e-posta kutusundan okur. Task<string> ile token doner. private oldugu icin xUnit bunu bagimsiz test saymaz.
- AcceptanceTestClock.GetUtcNow(): public override ile TimeProvider'in saatini testte degistirir. DateTimeOffset doner; sistem saatini degistirmez.

Ilk davranis testi endpoint yokken beklenen 404 nedeniyle RED oldu, uygulamadan sonra GREEN oldu. Sonraki sinir testleri mevcut uygulamayi genisleterek dogruladi; hepsi icin ayrica production-bug kaynakli RED goruldugu iddia edilmiyor.
Bir testin hazirliginda register icin yanlis 200 beklentisi vardi; mevcut sozlesme 201 oldugu icin test duzeltildi. Production cevabi test gecsin diye degistirilmedi.

## Dogrulama Sonucu

2026-09-06: 267 test basarili, 0 basarisiz, 0 atlanan. Kabul dosyasinda 9 test vakasi var. Build: 0 uyari, 0 hata. Slopwatch: 0 bulgu. Salt okunur alt ajan incelemesinde uygulanabilir hata bulunmadi. Eszamanlilik testi iki istegi birlikte baslatir fakat transaction'larin mutlaka ayni anda kilide ulasmasini zorlamaz; bu sinir saklanmamalidir.

Gercekte uygulanan skill'ler: tdd (HTTP davranis testleri ve ilk red/green), efcore-patterns (AsNoTracking, EF CLI migration ve transaction siniri), dotnet-slopwatch (uyari bastirma/test kapatma gibi kestirmelerin kontrolu).

## Kapsam Sinirlari

- Davet endpoint'i mevcut hesabi sifirlamaz veya rolu degistirmez. Parola unutma AUTH-006 kapsamindadir.
- InvitedById, CreatedAtUtc, RevokedAtUtc, AcceptedAtUtc ve AcceptedUserId gecmisi korunur. Bunlar audit icin hazir metadata'dir; ayri genel audit log/event sistemi bu dilimde eklenmedi.
- HTTPS, dis SMTP teslimi, rate limiting ve tarayici formu gibi production gereksinimleri bu dilimin test sonucu olarak sunulmaz.
- Commit, push ve tag bu dilimde yapilmadi.

## Tam Dosyalar

### C:/Users/Hamza/Documents/OpsDesk API/src/OpsDesk.Application/Invitations/DTOs/AcceptInvitationRequest.cs

```csharp
namespace OpsDesk.Application.Invitations.DTOs;

public sealed record AcceptInvitationRequest(
    string Token, string FirstName, string LastName, string Password);
```

### C:/Users/Hamza/Documents/OpsDesk API/src/OpsDesk.Application/Invitations/DTOs/AcceptedInvitationResponse.cs

```csharp
using OpsDesk.Domain.Enums;

namespace OpsDesk.Application.Invitations.DTOs;

public sealed record AcceptedInvitationResponse(
    Guid UserId, string Email, UserRole Role, DateTime EmailVerifiedAtUtc);
```

### C:/Users/Hamza/Documents/OpsDesk API/src/OpsDesk.Application/Invitations/Interfaces/IInvitationService.cs

```csharp
using OpsDesk.Application.Invitations.DTOs;

namespace OpsDesk.Application.Invitations.Interfaces;

public interface IInvitationService
{
    // Creates a verified account with the email and role stored in a valid invitation.
    Task<AcceptedInvitationResponse> AcceptAsync(
        AcceptInvitationRequest request, CancellationToken cancellationToken = default);

    // Creates one invitation for the authenticated inviter and sends its token by email.
    Task<InvitationResponse> CreateAsync(
        CreateInvitationRequest request, Guid invitedById,
        CancellationToken cancellationToken = default);
}
```

### C:/Users/Hamza/Documents/OpsDesk API/src/OpsDesk.Application/Invitations/Interfaces/IInvitationRepository.cs

```csharp
using OpsDesk.Domain.Entities;

namespace OpsDesk.Application.Invitations.Interfaces;

public interface IInvitationRepository
{
    // Reads invitation metadata without tracking changes; acceptance rechecks state atomically.
    Task<UserInvitation?> GetByTokenHashAsync(string tokenHash,
        CancellationToken cancellationToken = default);

    // Atomically consumes an active invitation and inserts its new User; false means it is no longer valid.
    Task<bool> TryAcceptAsync(Guid invitationId, User user, DateTime acceptedAtUtc,
        CancellationToken cancellationToken = default);

    // Atomically retires expired invitations and persists a new pending invitation.
    Task AddAsync(UserInvitation invitation, CancellationToken cancellationToken = default);

    // Retires an undelivered invitation so the admin can retry safely.
    Task RevokeAsync(Guid invitationId, DateTime revokedAtUtc,
        CancellationToken cancellationToken = default);
}
```

### C:/Users/Hamza/Documents/OpsDesk API/src/OpsDesk.Application/Invitations/Interfaces/IInvitationTokenGenerator.cs

```csharp
using OpsDesk.Application.Invitations.Models;

namespace OpsDesk.Application.Invitations.Interfaces;

public interface IInvitationTokenGenerator
{
    // Generates a purpose-specific token and its storage hash.
    GeneratedInvitationToken GenerateToken();

    // Rejects malformed or wrong-purpose tokens before computing their lookup hash.
    string ComputeHash(string rawToken);
}
```

### C:/Users/Hamza/Documents/OpsDesk API/src/OpsDesk.Application/Invitations/Services/InvitationService.cs

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
    IPasswordValidator passwordValidator,
    IPasswordHasher passwordHasher,
    TimeProvider timeProvider) : IInvitationService
{
    // Validates recipient input, binds identity to the invitation, and atomically creates the account.
    public async Task<AcceptedInvitationResponse> AcceptAsync(
        AcceptInvitationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        string hash = tokens.ComputeHash(request.Token);
        UserInvitation? invitation = await invitations.GetByTokenHashAsync(hash, cancellationToken);
        if (invitation is null || !invitation.CanAcceptAt(timeProvider.GetUtcNow().UtcDateTime))
        {
            throw new ArgumentException("Invitation token is invalid or expired.");
        }

        passwordValidator.Validate(request.Password);
        string firstName = UserInputNormalizer.NormalizeName(request.FirstName, nameof(request.FirstName));
        string lastName = UserInputNormalizer.NormalizeName(request.LastName, nameof(request.LastName));
        string passwordHash = passwordHasher.HashPassword(request.Password);
        DateTime acceptedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        if (!invitation.CanAcceptAt(acceptedAtUtc))
        {
            throw new ArgumentException("Invitation token is invalid or expired.");
        }

        var user = new User
        {
            FirstName = firstName,
            LastName = lastName,
            Email = invitation.Email,
            Role = invitation.Role,
            PasswordHash = passwordHash,
            CreatedAtUtc = acceptedAtUtc
        };
        user.MarkEmailVerified(acceptedAtUtc);

        bool accepted = await invitations.TryAcceptAsync(invitation.Id, user, acceptedAtUtc, cancellationToken);
        if (!accepted)
        {
            throw new ArgumentException("Invitation token is invalid or expired.");
        }

        return new AcceptedInvitationResponse(user.Id, user.Email, user.Role, acceptedAtUtc);
    }

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

### C:/Users/Hamza/Documents/OpsDesk API/src/OpsDesk.Domain/Entities/UserInvitation.cs

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
    public DateTime? AcceptedAtUtc { get; private set; }
    public Guid? AcceptedUserId { get; private set; }

    private UserInvitation() { }

    // A revoked, consumed, not-yet-valid, or expired invitation cannot create an account.
    public bool CanAcceptAt(DateTime acceptedAtUtc)
    {
        if (acceptedAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Acceptance time must be UTC.", nameof(acceptedAtUtc));
        }

        return RevokedAtUtc is null && AcceptedAtUtc is null
            && acceptedAtUtc >= CreatedAtUtc && acceptedAtUtc < ExpiresAtUtc;
    }

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

### C:/Users/Hamza/Documents/OpsDesk API/src/OpsDesk.Infrastructure/Authentication/InvitationTokenGenerator.cs

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
        return new GeneratedInvitationToken(rawToken, ComputeHash(rawToken));
    }

    // Accepts only the URL-safe invitation format; verification tokens have no inv_ prefix.
    public string ComputeHash(string rawToken)
    {
        if (string.IsNullOrWhiteSpace(rawToken) || rawToken.Length != 47
            || !rawToken.StartsWith("inv_", StringComparison.Ordinal)
            || rawToken[4..].Any(character =>
                !char.IsAsciiLetterOrDigit(character) && character is not ('_' or '-')))
        {
            throw new ArgumentException("Invitation token is invalid or expired.");
        }

        return generator.ComputeHash(rawToken);
    }
}
```

### C:/Users/Hamza/Documents/OpsDesk API/src/OpsDesk.Infrastructure/Persistence/Repositories/InvitationRepository.cs

```csharp
using Microsoft.EntityFrameworkCore;
using Npgsql;
using OpsDesk.Application.Common.Exceptions;
using OpsDesk.Application.Invitations.Interfaces;
using OpsDesk.Domain.Entities;

namespace OpsDesk.Infrastructure.Persistence.Repositories;

public sealed class InvitationRepository(OpsDeskDbContext database) : IInvitationRepository
{
    // Loads a read-only invitation; the write path must recheck its state under a database lock.
    public Task<UserInvitation?> GetByTokenHashAsync(string tokenHash,
        CancellationToken cancellationToken = default)
    {
        return database.UserInvitations.AsNoTracking()
            .SingleOrDefaultAsync(invitation => invitation.TokenHash == tokenHash, cancellationToken);
    }

    // The conditional update locks the row; account creation and acceptance commit or roll back together.
    public async Task<bool> TryAcceptAsync(Guid invitationId, User user, DateTime acceptedAtUtc,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        int affectedRows = await database.UserInvitations
            .Where(invitation => invitation.Id == invitationId
                && invitation.Email == user.Email && invitation.Role == user.Role
                && invitation.RevokedAtUtc == null && invitation.AcceptedAtUtc == null
                && invitation.CreatedAtUtc <= acceptedAtUtc && invitation.ExpiresAtUtc > acceptedAtUtc)
            .ExecuteUpdateAsync(setters => setters.SetProperty(
                invitation => invitation.AcceptedAtUtc, acceptedAtUtc), cancellationToken);
        if (affectedRows == 0)
        {
            return false;
        }

        database.Users.Add(user);
        try
        {
            await database.SaveChangesAsync(cancellationToken);
            await database.UserInvitations.Where(invitation => invitation.Id == invitationId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(
                    invitation => invitation.AcceptedUserId, user.Id), cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "ux_users_email"
        })
        {
            throw new ConflictException("An account with this email already exists.", exception);
        }
    }

    // Retires expired records and saves the replacement within one transaction.
    public async Task AddAsync(UserInvitation invitation, CancellationToken cancellationToken = default)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        await database.UserInvitations
            .Where(x => x.Email == invitation.Email && x.RevokedAtUtc == null && x.AcceptedAtUtc == null
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
        await database.UserInvitations.Where(x => x.Id == invitationId
                && x.RevokedAtUtc == null && x.AcceptedAtUtc == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(
                x => x.RevokedAtUtc, revokedAtUtc), cancellationToken);
    }
}
```

### C:/Users/Hamza/Documents/OpsDesk API/src/OpsDesk.Infrastructure/Persistence/Configurations/UserInvitationConfiguration.cs

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
        builder.Property(x => x.AcceptedAtUtc).HasColumnName("accepted_at_utc");
        builder.Property(x => x.AcceptedUserId).HasColumnName("accepted_user_id");
        builder.HasIndex(x => x.Email).IsUnique()
            .HasFilter("revoked_at_utc IS NULL AND accepted_at_utc IS NULL")
            .HasDatabaseName("ux_user_invitations_pending_email");
        builder.HasIndex(x => x.TokenHash).IsUnique();
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.InvitedById)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.AcceptedUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
```

### C:/Users/Hamza/Documents/OpsDesk API/src/OpsDesk.Infrastructure/Persistence/Migrations/20260906122925_AddInvitationAcceptance.cs

```csharp
using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpsDesk.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInvitationAcceptance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_user_invitations_pending_email",
                table: "user_invitations");

            migrationBuilder.AddColumn<DateTime>(
                name: "accepted_at_utc",
                table: "user_invitations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "accepted_user_id",
                table: "user_invitations",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_invitations_accepted_user_id",
                table: "user_invitations",
                column: "accepted_user_id");

            migrationBuilder.CreateIndex(
                name: "ux_user_invitations_pending_email",
                table: "user_invitations",
                column: "email",
                unique: true,
                filter: "revoked_at_utc IS NULL AND accepted_at_utc IS NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_user_invitations_users_accepted_user_id",
                table: "user_invitations",
                column: "accepted_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_user_invitations_users_accepted_user_id",
                table: "user_invitations");

            migrationBuilder.DropIndex(
                name: "IX_user_invitations_accepted_user_id",
                table: "user_invitations");

            migrationBuilder.DropIndex(
                name: "ux_user_invitations_pending_email",
                table: "user_invitations");

            migrationBuilder.DropColumn(
                name: "accepted_at_utc",
                table: "user_invitations");

            migrationBuilder.DropColumn(
                name: "accepted_user_id",
                table: "user_invitations");

            migrationBuilder.CreateIndex(
                name: "ux_user_invitations_pending_email",
                table: "user_invitations",
                column: "email",
                unique: true,
                filter: "revoked_at_utc IS NULL");
        }
    }
}
```

### C:/Users/Hamza/Documents/OpsDesk API/src/OpsDesk.Api/Controllers/InvitationsController.cs

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpsDesk.Application.Invitations.DTOs;
using OpsDesk.Application.Invitations.Interfaces;

namespace OpsDesk.Api.Controllers;

[ApiController]
[Route("auth/invitations")]
public sealed class InvitationsController(IInvitationService invitations) : ControllerBase
{
    // Allows a recipient without an account to accept the invitation using its secret token.
    [AllowAnonymous]
    [HttpPost("accept")]
    [ProducesResponseType<AcceptedInvitationResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AcceptedInvitationResponse>> Accept(
        AcceptInvitationRequest request, CancellationToken cancellationToken)
    {
        AcceptedInvitationResponse response = await invitations.AcceptAsync(request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, response);
    }
}
```

### C:/Users/Hamza/Documents/OpsDesk API/tests/OpsDesk.Tests/Integration/InvitationAcceptanceIntegrationTests.cs

```csharp
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpsDesk.Application.Auth.DTOs;

namespace OpsDesk.Tests.Integration;

[Collection(IntegrationTestCollection.Name)]
public sealed class InvitationAcceptanceIntegrationTests(OpsDeskApiFixture fixture)
{
    // An anonymous recipient accepts once, then logs in with the invited role and verified access.
    [Theory]
    [InlineData("agent", "Agent")]
    [InlineData("customer", "Customer")]
    public async Task Accept_should_create_verified_account_and_reject_replay(string role, string loginRole)
    {
        using HttpClient client = fixture.Factory.CreateClient();
        string email = $"accept-{Guid.NewGuid():N}@example.com";
        string token = await InviteAsync(client, email, role);
        var request = new { token, firstName = "Invited", lastName = "Agent", password = "ValidPass!" };

        using HttpResponseMessage response = await client.PostAsJsonAsync("/auth/invitations/accept", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(email, body.RootElement.GetProperty("email").GetString());
        Assert.Equal(role, body.RootElement.GetProperty("role").GetString());
        Assert.False(body.RootElement.TryGetProperty("passwordHash", out _));
        Assert.False(body.RootElement.TryGetProperty("accessToken", out _));

        using HttpResponseMessage replay = await client.PostAsJsonAsync("/auth/invitations/accept", request);
        Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);
        using HttpResponseMessage login = await client.PostAsJsonAsync(
            "/auth/login", new LoginRequest(email, "ValidPass!"));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        AuthResponse? account = await login.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(account);
        Assert.Equal(loginRole, account.Role);
        Assert.Equal(body.RootElement.GetProperty("userId").GetGuid(), account.UserId);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", account.AccessToken);
        using HttpResponseMessage tickets = await client.GetAsync("/tickets");
        Assert.Equal(HttpStatusCode.OK, tickets.StatusCode);
    }

    // Invalid input must leave the invitation usable; extra identity fields cannot override its scope.
    [Theory]
    [InlineData("Invited", "Person", "short")]
    [InlineData(" ", "Person", "ValidPass!")]
    [InlineData("Invited", " ", "ValidPass!")]
    public async Task Invalid_input_should_allow_corrected_retry(string firstName, string lastName, string password)
    {
        using HttpClient client = fixture.Factory.CreateClient();
        string email = $"retry-{Guid.NewGuid():N}@example.com";
        string token = await InviteAsync(client, email, "customer");
        using HttpResponseMessage invalid = await client.PostAsJsonAsync("/auth/invitations/accept",
            new { token, firstName, lastName, password });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        using HttpResponseMessage corrected = await client.PostAsJsonAsync("/auth/invitations/accept",
            new { token, firstName = "Invited", lastName = "Person", password = "ValidPass!",
                email = "attacker@example.com", role = "admin" });
        Assert.Equal(HttpStatusCode.Created, corrected.StatusCode);
        using JsonDocument body = JsonDocument.Parse(await corrected.Content.ReadAsStringAsync());
        Assert.Equal(email, body.RootElement.GetProperty("email").GetString());
        Assert.Equal("customer", body.RootElement.GetProperty("role").GetString());
    }

    // Two simultaneous requests must create only one account and consume the token once.
    [Fact]
    public async Task Concurrent_acceptance_should_have_one_winner()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        string email = $"race-{Guid.NewGuid():N}@example.com";
        string token = await InviteAsync(client, email);
        var request = new { token, firstName = "Invited", lastName = "Person", password = "ValidPass!" };
        HttpResponseMessage[] responses = await Task.WhenAll(
            client.PostAsJsonAsync("/auth/invitations/accept", request),
            client.PostAsJsonAsync("/auth/invitations/accept", request));
        try
        {
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.BadRequest);
            using HttpResponseMessage login = await client.PostAsJsonAsync(
                "/auth/login", new LoginRequest(email, "ValidPass!"));
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        }
        finally
        {
            foreach (HttpResponseMessage response in responses) response.Dispose();
        }
    }

    // A conflicting public registration cannot have its password or role overwritten by acceptance.
    [Fact]
    public async Task Existing_account_should_remain_unchanged_and_acceptance_should_roll_back()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        string email = $"existing-{Guid.NewGuid():N}@example.com";
        string token = await InviteAsync(client, email);
        using HttpResponseMessage registration = await client.PostAsJsonAsync("/auth/register",
            new RegisterRequest("Original", "Person", email, "OriginalPass!"));
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
        var request = new { token, firstName = "Invited", lastName = "Person", password = "ChangedPass!" };
        using HttpResponseMessage first = await client.PostAsJsonAsync("/auth/invitations/accept", request);
        Assert.Equal(HttpStatusCode.Conflict, first.StatusCode);
        using HttpResponseMessage retry = await client.PostAsJsonAsync("/auth/invitations/accept", request);
        Assert.Equal(HttpStatusCode.Conflict, retry.StatusCode);

        using HttpResponseMessage login = await client.PostAsJsonAsync(
            "/auth/login", new LoginRequest(email, "OriginalPass!"));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        AuthResponse? account = await login.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(account);
        Assert.Equal("Customer", account.Role);
        Assert.Equal("Original", account.FirstName);
        using HttpResponseMessage changedPassword = await client.PostAsJsonAsync(
            "/auth/login", new LoginRequest(email, "ChangedPass!"));
        Assert.Equal(HttpStatusCode.Unauthorized, changedPassword.StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", account.AccessToken);
        using HttpResponseMessage tickets = await client.GetAsync("/tickets");
        Assert.Equal(HttpStatusCode.Forbidden, tickets.StatusCode);
    }

    // An email-verification token must not act as an invitation, even when it has a valid format.
    [Fact]
    public async Task Verification_and_unknown_tokens_should_not_accept_invitation()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        string email = $"purpose-{Guid.NewGuid():N}@example.com";
        using HttpResponseMessage registration = await client.PostAsJsonAsync("/auth/register",
            new RegisterRequest("Test", "Person", email, "ValidPass!"));
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
        string verificationToken = Assert.Single(fixture.Factory.SentEmails,
            message => message.RecipientEmail == email).RawToken;
        string[] invalidTokens = [verificationToken, "inv_" + verificationToken, "inv_invalid", ""];
        foreach (string token in invalidTokens)
        {
            using HttpResponseMessage response = await client.PostAsJsonAsync("/auth/invitations/accept",
                new { token, firstName = "Invited", lastName = "Person", password = "ValidPass!" });
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        using HttpResponseMessage confirm = await client.PostAsJsonAsync("/auth/email-verification/confirm",
            new { token = verificationToken });
        Assert.Equal(HttpStatusCode.NoContent, confirm.StatusCode);
    }

    // Moving the application clock verifies exact expiry and prevents an old token replacing a fresh one.
    [Fact]
    public async Task Expired_and_replaced_tokens_should_be_rejected_but_new_token_should_work()
    {
        var clock = new AcceptanceTestClock(DateTimeOffset.UtcNow);
        using var factory = fixture.Factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(clock);
        }));
        using HttpClient client = factory.CreateClient();
        string email = $"expiry-{Guid.NewGuid():N}@example.com";
        string oldToken = await InviteAsync(client, email);
        clock.Now = clock.Now.AddHours(24);
        var oldRequest = new { token = oldToken, firstName = "Invited", lastName = "Person", password = "ValidPass!" };
        using HttpResponseMessage expired = await client.PostAsJsonAsync("/auth/invitations/accept", oldRequest);
        Assert.Equal(HttpStatusCode.BadRequest, expired.StatusCode);
        string newToken = await InviteAsync(client, email);
        using HttpResponseMessage retired = await client.PostAsJsonAsync("/auth/invitations/accept", oldRequest);
        Assert.Equal(HttpStatusCode.BadRequest, retired.StatusCode);
        using HttpResponseMessage accepted = await client.PostAsJsonAsync("/auth/invitations/accept",
            new { token = newToken, firstName = "Invited", lastName = "Person", password = "ValidPass!" });
        Assert.Equal(HttpStatusCode.Created, accepted.StatusCode);
    }

    private sealed class AcceptanceTestClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        // Supplies test-controlled UTC time without sleeping or changing the system clock.
        public override DateTimeOffset GetUtcNow() => Now;
    }

    // Creates an invitation through the real admin endpoint and returns the latest captured email token.
    private async Task<string> InviteAsync(HttpClient client, string email, string role = "agent")
    {
        using HttpResponseMessage login = await client.PostAsJsonAsync("/auth/login",
            new LoginRequest(OpsDeskApiFactory.AdminEmail, OpsDeskApiFactory.AdminPassword));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        AuthResponse? admin = await login.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(admin);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", admin.AccessToken);
        using HttpResponseMessage invitation = await client.PostAsJsonAsync(
            "/admin/invitations", new { email, role });
        Assert.Equal(HttpStatusCode.Created, invitation.StatusCode);
        client.DefaultRequestHeaders.Authorization = null;
        return fixture.Factory.SentInvitations.Last(
            message => message.RecipientEmail == email).RawToken;
    }
}
```
