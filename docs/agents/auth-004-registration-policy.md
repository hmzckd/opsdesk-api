# AUTH-004: Ortama Gore Acik Kayit Politikasi

2026-09-07. Onaylanan kapsam uygulandi. 303 test basarili, 0 basarisiz/atlanan; build 0 uyari/hata; Slopwatch 0 bulgu.
Bu belge yeni metotlari, parametreleri ve katman tercihlerini aciklar. Sondaki dosyalar tam inceleme kopyalaridir; guncel kaynak src/ ve tests/ altindadir.

## Task'in Amaci

Gercek kullanimda rastgele ziyaretcilerin hesap acmasini engellemek; gelistirme kaydini ve admin'in davet/hesap olusturma akislarini korumak.

## Calisan Davranis

| Ortam / ayar | Sonuc |
| --- | --- |
| Ayar yok | Varsayilan false: acik kayit kapali |
| Development dosyasindaki true | Mevcut register akisi calisir |
| Demo gibi baska bir non-production ortaminda acikca true | Register calisir |
| Herhangi bir ortamda false | Gecerli register istegi 403, hesap/email/JWT uretilmez |
| Production ortaminda true | Uygulama baslamaz; ayar hatasi verilir |
| true/false yerine gecersiz metin | Uygulama baslamaz; ayar donusum hatasi verilir |

Production, uygulamanin gercek kullanicilara hizmet verdigi ortam adidir. Isletim sistemi, internete acik IP veya Docker kullanimi kendiliginden Production secmez.
Sunucu ortam adini yanlislikla Development verirsen Development davranisi gorursun; deployment ortam adini dogru secmelidir.
Development dosyasi zaten true verdigi icin normal yerel register deneyimin korunur. Genel appsettings.json false tutar.
Test host'u Testing adini kullanir ve kaydi acikca true yapar; eski register testleri tesadufen Development ayarina baglanmaz.

Ayar uygulama omru boyunca sabittir; dosya/environment degisikliginden sonra API yeniden baslatilmalidir.
Kapanan yalnizca POST /auth/register ile self-registration'dir. Login, email confirmation, sifre kurtarma/sifirlama, admin daveti/kabulu ve admin Agent olusturma calisir.
Mevcut kullanicilar silinmez, JWT'ler bu ayar yuzunden iptal edilmez, parola ile giris kapatilmaz.
Swagger'da endpoint gorunur; saklamak guvenlik kontrolu degildir. Kapanma Application servisinde uygulanir.
Bozuk JSON/eksik zorunlu alan gibi HTTP model-binding hatalari servise ulasmadan 400 alabilir; 403 sozlesmesi gecerli register istekleri icindir.

## Akis

appsettings / environment / Development User Secrets
-> Api RegistrationConfiguration
-> IOptions<RegistrationSettings> ile ayari oku ve dogrula
-> dogrulanmis RegistrationSettings nesnesini DI'a ver
-> AuthService constructor kayit acik bilgisini alir.

HTTP POST /auth/register
-> AuthController.Register
-> AuthService.RegisterAsync
-> kapaliysa ForbiddenException
-> mevcut GlobalExceptionHandler
-> 403 Problem Details.

Aciksa onceki akis aynen devam eder:
input validation -> email kontrolu -> password hash -> User olusturma -> DB -> verification email -> JWT/AuthResponse -> 201.

Controller disindan IAuthService.RegisterAsync cagirilsa da ayni kontrol calisir.
User entity'sini veya repository'nin tum Add islemlerini kapatmadik: davet ve admin Agent olusturma da ayni users tablosuna yazmak zorundadir.

## Dosyalar ve Katmanlar

Tum kaynak basliklari tam yollariyla asagidadir. Projeler src altinda kardes klasorlerdir.

- src/OpsDesk.Application/Auth/Models/RegistrationSettings.cs: YENI. Kayit akisinin ihtiyac duydugu tipli ayar. HTTP, EF veya Options paketi bilmez.
- src/OpsDesk.Api/Configuration/RegistrationConfiguration.cs: YENI. Configuration klasoru host ayarlarini toplamak icin eklendi; environment, binding ve DI burada kalir.
- src/OpsDesk.Application/Auth/Services/AuthService.cs: constructor'a settings eklenir, RegisterAsync basinda kontrol yapilir.
- src/OpsDesk.Api/Controllers/AuthController.cs: Swagger icin 201/403 cevap metadata'si eklenir; is kurali Controller'a tasinmaz.
- src/OpsDesk.Api/Program.cs: AddRegistrationConfiguration cagrisi ve DB hazirligindan once erken ayar dogrulamasi.
- src/OpsDesk.Api/appsettings.json: Registration:PublicRegistrationEnabled=false.
- src/OpsDesk.Api/appsettings.Development.json: ayni ayar Development icin true.
- tests/OpsDesk.Tests/Integration/OpsDeskApiFactory.cs: test kayit akisi icin acik true ayari.
- tests/OpsDesk.Tests/Integration/RegistrationPolicyIntegrationTests.cs: YENI. 13 senaryo/parametre kombinasyonu.
- README.md: deploy/yerel ayar dokumani ve register endpoint aciklamasi.
- docs/tasks/opsdesk-task-ledger.md: onayli sozlesme, Given/When/Then, test kaniti ve Review durumu.
- docs/agents/auth-004-registration-policy.md: bu aciklama ve tam C# kaynak kopyalari.

Domain, Infrastructure, DB tablolari ve mevcut DTO'lar degismedi. Yeni paket veya migration yok.
Git'teki diger auth degisiklikleri onceki task'lardan da gelebilir; tum uncommitted diff'i AUTH-004'te yazilmis sayma.

## RegistrationSettings

public sealed class: diger katmanlarin kullanabildigi, miras alinmayan sinif.
SectionName sabiti Registration JSON bolumunun adidir. const string sabit metindir.
PublicRegistrationEnabled bool'dur: true veya false. bool'un varsayilani false oldugu icin ayar belirtilmezse kayit acilmaz.
get/set, .NET configuration binder'in bu nesneye ayari yazabilmesini saglar.
Bu nesne request DTO degildir: istemci register JSON'una PublicRegistrationEnabled=true koyarak sunucu ayarini degistiremez.
Application/Auth/Models altindadir cunku kayit use-case'inin girdisi olan sunucu politikasini temsil eder; kullaniciya veya DB'ye ait bir entity degildir.

## AddRegistrationConfiguration Metodu

Tam imza: public static IServiceCollection AddRegistrationConfiguration(this IServiceCollection services).

public: Program.cs tarafindan cagirilabilir.
static: RegistrationConfiguration nesnesi olusturmadan kullanilir.
this IServiceCollection services: extension method olmasini saglar; builder.Services.AddRegistrationConfiguration() diye cagirabiliriz.
services parametresi DI kayit listesidir. Donus tipi IServiceCollection ayni listeyi geri verir; yeni kullanici veya HTTP cevabi degildir.
Asenkron is yapmadigi icin async/Task yoktur.

Govde adimlari:
1. AddOptions<RegistrationSettings>() bu tipe ait ayar altyapisini kaydeder.
2. BindConfiguration(RegistrationSettings.SectionName) Registration bolumunu nesneye baglar.
3. Validate<IHostEnvironment> ayar ve ortam bilgisini birlikte kontrol eder.
4. ValidateOnStart ayar dogrulamasini host baslangicina baglar.
5. AddSingleton ile dogrulanmis Value nesnesini Application'a verilecek sekilde DI'a ekler.
6. return services ile kayit listesini dondurur.

Validate<IHostEnvironment> icindeki generic tur, kontrol icin DI'dan ortam bilgisinin de alinacagini soyler.
(settings, environment) => ifadesi isimsiz kucuk bir fonksiyondur (lambda); girdileri ayar ve host ortamidir, cikti bool'dur.
!environment.IsProduction() || !settings.PublicRegistrationEnabled:
! "degil", || "veya" demektir. Production DEGILSE veya kayit ACIK DEGILSE ayar gecerlidir.
Dolayisiyla Production + true kombinasyonu false doner ve OptionsValidationException olusur.

IOptions<T>, .NET'in tipli ayar kutusudur; T burada RegistrationSettings. .Value bu kutudaki dogrulanmis ayar nesnesidir.
GetRequiredService<IOptions<RegistrationSettings>>() DI'dan bu kutuyu ister; kaydi yoksa sessiz null yerine hata verir.
AddSingleton(provider => ...) icindeki provider DI konteyneridir; lambda ihtiyac aninda ayar nesnesini uretir/alir ve tek nesne kullanilir.
AuthService'e IOptions paketi eklemek yerine ayni dogrulanmis duz C# nesnesini veriyoruz; Application'in .csproj bagimliliklari buyumuyor.
Bu metot Api'dedir cunku .NET host ortami/configuration altyapisini bilir. Domain'in Production diye bir kavram bilmesi gerekmez.

## Program.cs Erken Kontrolu

builder.Services.AddRegistrationConfiguration() kayitlari tanimlar. Bu satir tek basina ayar nesnesini hemen okumaz.
builder.Build() sonrasindaki IOptions<RegistrationSettings>.Value erisimi okumayi/dogrulamayi zorlar.
_ = ifadesindeki _ discard'dir: sonucu degiskende kullanmayacagiz, fakat erisimin yapilmasini ve dogrulamanin calismasini istiyoruz.
Bu satir mevcut migration/seed blogundan ONCEDIR. Production ayari yanlissa DB hazirligina gecilmez.
ValidateOnStart da korunur; dogrulama yalnizca ilk register istegi geldiginde yapilmaz.
Yeni bir environment tespit yontemi yazilmadi; ASP.NET Core'un IHostEnvironment bilgisi kullanilir.

## AuthService Constructor

Eklenen parametre: RegistrationSettings registrationSettings.
Constructor sinif olusturulurken DI tarafindan cagrilir. Bu bir HTTP parametresi veya register request alani degildir.
_publicRegistrationEnabled = registrationSettings.PublicRegistrationEnabled ile o servis nesnesi icin bool saklanir.
private readonly bool: yalnizca sinifin icinden gorulur ve constructor disinda yeniden atanmaz.
Mevcut repository, hasher, JWT generator, parola/email validator ve verification service parametreleri degismeden korunur.
Ayarin geldigini bilmek yeterlidir; servis JSON dosyasi okumaz, environment variable okumaz veya Production adini kontrol etmez.

## AuthService.RegisterAsync

Tam imza: public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default).

public: IAuthService uzerinden Controller veya baska bir cagiran kullanabilir.
async: DB/email gibi beklemeli adimlarda await vardir; ayar kontrolu ise anlik/senkron calisir.
Task<AuthResponse>: tamamlaninca AuthResponse DONDURUR; AuthResponse bir parametre DEGILDIR.
request: istemcinin ad/soyad/email/parola verilerini tasiyan RegisterRequest.
cancellationToken: HTTP istegi iptal edilirse DB ve email beklemelerine iptal sinyali tasir; JWT veya parola kodu degildir.
= default: dogrudan cagiran bir iptal sinyali vermezse varsayilan token kullanilir.

Eklenen ilk blok ayar kapaliysa ForbiddenException firlatir. Bu noktada hesap sorgusu, hash hesaplama, kayit, email veya JWT yoktur.
Kapali durumda Task basarili AuthResponse ile tamamlanmaz; hata ile tamamlanir.
Mevcut exception handler ForbiddenException'i 403 Problem Details'e cevirir. Yeni exception tipi veya hata formati uretilmedi.
Acik durumda input kontrolu, normalization, mevcut email sorgusu ve eskiden yazilan kayit islemleri aynen devam eder.
Kontrol Application'dadir cunku "bu kayit islemi yapilabilir mi?" sorusunu cozer. Api'de olsaydi servis dogrudan cagrilarak atlanabilirdi.
Repository'de tum kullanici eklemelerine konulsaydi admin/davet akisini da yanlislikla kapatirdi.

## AuthController.Register

Govde degismedi. Mevcut public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request, CancellationToken cancellationToken) metodu kullanilir.
ActionResult<AuthResponse> HTTP sonucu ve tipli cevap destegini saglar; Task sonucu asenkron hazirlar.
Controller request'i service'e iletir ve basarida 201 doner.
Eklenen ProducesResponseType attribute'lari 201 AuthResponse ve 403 ProblemDetails olasiliklarini Swagger'a anlatir.
Attribute kodu tek basina 403 uretmez; asil kontrol AuthService, HTTP hata cevabi GlobalExceptionHandler'dadir.
AllowAnonymous korunur: acik kayitta henuz hesabi olmayan kisi gelebilmeli; kapali davranisi servis politikasi belirler.

## Testleri Tek Tek Okuma

Testler Tests katmanindadir; API'nin davranisini disaridan dogrular, uygulama istegi geldikce production'da calismaz.
Fact bir senaryo, Theory parametreli senaryo, InlineData her bir girdidir. Bu sinifta 13 test durumu vardir.
Asagidaki async metotlar Task doner: xUnit bitmelerini bekler, kullaniciya DTO dondurmezler.
Testler gercek Application/Infrastructure'u ve Docker'da gecici PostgreSQL'i kullanir; e-posta gonderimi test mailbox'inda yakalanir.

### Disabled_registration_should_reject_the_request_without_creating_an_account
public async Task, parametresiz. Host ayarini false yapar; register 403, ayni email/parola login 401, verification email yok bekler.
Ilk RED: beklenen Forbidden, gercek Created. Service kontrolu eklendikten sonra GREEN.

### Production_with_enabled_registration_should_fail_startup
public void, parametresiz; host olusturma senkron oldugu icin Task yok.
Production + true ile factory.CreateClient uygulamayi baslatmaya calisir. Assert.Throws<OptionsValidationException> ve acik hata metni beklenir.
Ilk RED: hic exception yoktu. Validate kuralindan sonra GREEN.

### Environment_configuration_should_control_public_registration
public async Task; parametreler string environment, string? enabled, HttpStatusCode expected.
environment host adidir; enabled "true", "false" veya null olabilir. ? ayarin bulunmamasini/null olabilmesini ifade eder.
expected InlineData'nin verdigi beklenen HTTP kodudur; metodun donus tipi degildir.
Development/Demo true ile 201, Development/Production false ile 403, Production/Staging null ile 403 test edilir.
Ardindan login ve email davranisi kontrol edilir. Production host'unda migration calistirmamak icin gecici schema once Testing host'uyla hazirlanir.
Assert ile gercek IHostEnvironment adinin beklenen ortam oldugu da dogrulanir.

### Disabled_registration_should_also_reject_direct_service_calls
public async Task. factory.Services.CreateScope isteklik servisleri almak icin gecici DI omru acar.
GetRequiredService<IAuthService>() ile gercek public Application sozlesmesi cagrilir; ForbiddenException beklenir.
HTTP Controller'a bagli kalmayan korumayi kanitlar; private metot cagrilari veya mock cagri sayilari test edilmez.

### Disabled_registration_should_preserve_invitation_login_and_password_recovery
public async Task. Production + kayit kapali host kullanir; test setup'i admin hesabini once Testing ortaminda hazirlar.
Admin login -> davet olustur -> anonim davet kabul -> davetli login -> forgot -> emailden reset -> yeni parola login -> tickets 200.
Yetki header'i her kimlik degisiminde duzenlenir. Davetli Customer rolunu ve dogrulanmis email erisimini korur.
DB'de kayit acmak icin public register'i dolasmaya calisan kestirme degil, ayri onayli davet use-case'i kullanilir.

### Disabled_registration_should_preserve_admin_agent_provisioning
public async Task. Kayit false iken admin login olur, POST /admin/agents ile Agent olusturur.
Yeni Agent login'i ve donen Agent rolu dogrulanir. Sunucuya ait rol karari korunur.

### Invalid_registration_setting_should_fail_startup
public void. Ayara not-a-boolean konur; factory.CreateClient sirasinda InvalidOperationException beklenir.
Bu, yeni production kodu gerektirmedi; configuration binder zaten bool donusumunu kontrol eder.

### Disabled_registration_should_preserve_existing_email_confirmation
public async Task. Kayit acikken hesap/e-posta olusturulur; kapali host'ta ayni verification koduyla confirm 204 alinir.
Hesabin JWT'siyle tickets 200 kontrol edilir. Kayit kapaninca bekleyen dogrulama linklerinin bozulmadigini gosterir.

OpsDeskApiFactory.ConfigureWebHost mevcut protected override void metottur: protected framework/miras erisimi, override WebApplicationFactory'nin davranisini degistirmedir.
Yeni eklenen yalnizca test konfigurasyonundaki Registration:PublicRegistrationEnabled=true satiridir. Production testleri bunu kendileri override eder.
Her test icin rastgele email kullanilir; var olan gercek kullanicilarin parolasi veya rolu degistirilmez.

## Test Akisi ve Kanit

Iki yeni davranis icin RED/GREEN goruldu: kapali register ve Production true ile baslangic hatasi.
Sonraki senaryolar GREEN olan implementation'a regresyon kapsami ekledi; hepsinin ayri bir production RED asamasi oldugu iddia edilmiyor.
13 yeni test, toplam 303 test basarili; hic test atlanmadi veya silinmedi.
Build: 0 uyari/hata. Slopwatch: 0 bulgu. Yeni paket, migration veya kalici gelistirme DB degisikligi yok.
Genis bir refactor veya birbirinden bagimsiz arastirma kollari olmadigi icin bu dar taskta alt ajan acilmadi.

## Yerel Deneme

API'yi Development'ta normal calistirinca register aciktir.
Kapatmak icin repo kokunde asagidaki komutu kullanip API'yi yeniden baslat:

```powershell
dotnet user-secrets set Registration:PublicRegistrationEnabled false --project src/OpsDesk.Api
```
Sonra gecerli register istegi 403 alir. Kapatma oncesi hesaplarin login'i calisir.
Override'i silip Development dosyasindaki true ayarina donmek icin:

```powershell
dotnet user-secrets remove Registration:PublicRegistrationEnabled --project src/OpsDesk.Api
```

Bu iki User Secrets komutu dokumantasyon ornegidir; kullanicinin yerel ayarini bu taskta degistirmedik.

Deploy ayari: Registration__PublicRegistrationEnabled=false. Environment variable icinde __, JSON'daki : bolum ayracina karsilik gelir.
Gercek host ortami Production secilmeli; demo icin baska bir non-production ortam adi ve acik true ayari gerekir.

Dogrulama komutlari, repo kokunde:

```powershell
dotnet build OpsDesk.sln --no-restore
```

```powershell
dotnet test OpsDesk.sln --no-build --no-restore
```

```powershell
dotnet slopwatch analyze --fail-on warning
```

## Gercekte Uygulanan Skill'ler

- tdd: public HTTP/service davranisinda RED/GREEN, sonra regresyon senaryolari.
- microsoft-extensions-configuration: BindConfiguration, IOptions, environment validation ve ValidateOnStart.
- dependency-injection-patterns: AddRegistrationConfiguration extension'inda kayitlari toplama, Application'a duz ayar nesnesi verme.
- dotnet-slopwatch: test/uyari gizleme taramasi.
- EF/migration skill'i kullanilmadi; veri modeli degismedi. Git yayini olmadigi icin commit odakli code-review akisi calistirilmadi.

## Tam Dosya Icerikleri

Bu dokuz C#/JSON dosyasi bu taskin kaynak degisiklikleridir; using ve namespace dahil tam kopyalari asagidadir.
README ve task defteri guncellemeleri kendi Markdown dosyalarindan incelenebilir.

### C:/Users/Hamza/Documents/OpsDesk API/src/OpsDesk.Application/Auth/Models/RegistrationSettings.cs

```csharp
namespace OpsDesk.Application.Auth.Models;

public sealed class RegistrationSettings
{
    public const string SectionName = "Registration";

    public bool PublicRegistrationEnabled { get; set; }
}
```

### C:/Users/Hamza/Documents/OpsDesk API/src/OpsDesk.Api/Configuration/RegistrationConfiguration.cs

```csharp
using Microsoft.Extensions.Options;
using OpsDesk.Application.Auth.Models;

namespace OpsDesk.Api.Configuration;

public static class RegistrationConfiguration
{
    // Binds host configuration while keeping the Application layer independent of Options packages.
    public static IServiceCollection AddRegistrationConfiguration(this IServiceCollection services)
    {
        services.AddOptions<RegistrationSettings>()
            .BindConfiguration(RegistrationSettings.SectionName)
            .Validate<IHostEnvironment>((settings, environment) =>
                !environment.IsProduction() || !settings.PublicRegistrationEnabled,
                "Registration:PublicRegistrationEnabled must be false in Production.")
            .ValidateOnStart();

        services.AddSingleton(provider =>
            provider.GetRequiredService<IOptions<RegistrationSettings>>().Value);
        return services;
    }
}
```

### C:/Users/Hamza/Documents/OpsDesk API/src/OpsDesk.Application/Auth/Services/AuthService.cs

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using OpsDesk.Application.Auth.DTOs;
using OpsDesk.Application.Auth.Interfaces;
using OpsDesk.Application.Auth.Models;
using OpsDesk.Application.Common.Exceptions;
using OpsDesk.Domain.Entities;
using OpsDesk.Domain.Enums;

namespace OpsDesk.Application.Auth.Services;

public sealed class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IPasswordValidator _passwordValidator;
    private readonly IEmailValidator _emailValidator;
    private readonly IEmailVerificationService _emailVerificationService;
    private readonly bool _publicRegistrationEnabled;

    public AuthService(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator jwtTokenGenerator,
        IPasswordValidator passwordValidator,
        IEmailValidator emailValidator,
        IEmailVerificationService emailVerificationService,
        RegistrationSettings registrationSettings)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _jwtTokenGenerator = jwtTokenGenerator;
        _passwordValidator = passwordValidator;
        _emailValidator = emailValidator;
        _emailVerificationService = emailVerificationService;
        _publicRegistrationEnabled = registrationSettings.PublicRegistrationEnabled;
    }

    // Rejects disabled self-registration before any account access; otherwise registers and sends verification.
    public async Task<AuthResponse> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_publicRegistrationEnabled)
        {
            throw new ForbiddenException("Public registration is disabled. Contact an administrator for an invitation.");
        }

        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Email);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Password);

        _emailValidator.Validate(request.Email);
        _passwordValidator.Validate(request.Password);

        string firstName = UserInputNormalizer.NormalizeName(
            request.FirstName,
            nameof(request.FirstName));

        string lastName = UserInputNormalizer.NormalizeName(
            request.LastName,
            nameof(request.LastName));

        string normalizedEmail =
            UserInputNormalizer.NormalizeEmail(request.Email);

        bool emailExists = await _userRepository.ExistsByEmailAsync(
            normalizedEmail,
            cancellationToken);

        if (emailExists)
        {
            throw new ConflictException(
                "A user with this email address already exists.");
        }

        var user = new User
        {
            FirstName = firstName,
            LastName = lastName,
            Email = normalizedEmail,
            PasswordHash = _passwordHasher.HashPassword(request.Password),
            Role = UserRole.Customer
        };

        await _userRepository.AddAsync(user, cancellationToken);

        await _emailVerificationService.IssueAsync(
            user,
            cancellationToken);

        return CreateAuthResponse(user);
    }

    public async Task<AuthResponse> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Email);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Password);

        _emailValidator.Validate(request.Email);

        string normalizedEmail =
            UserInputNormalizer.NormalizeEmail(request.Email);

        User? user = await _userRepository.GetByEmailAsync(
            normalizedEmail,
            cancellationToken);

        if (user is null ||
            !_passwordHasher.VerifyPassword(
                request.Password,
                user.PasswordHash))
        {
            throw new UnauthorizedAccessException(
                "Email or password is incorrect.");
        }

        return CreateAuthResponse(user);
    }

    private AuthResponse CreateAuthResponse(User user)
    {
        JwtTokenResult token =
            _jwtTokenGenerator.GenerateToken(user);

        return new AuthResponse(
            user.Id,
            user.FirstName,
            user.LastName,
            user.Email,
            user.Role.ToString(),
            token.AccessToken,
            token.ExpiresAtUtc);
    }
}
```

### C:/Users/Hamza/Documents/OpsDesk API/src/OpsDesk.Api/Controllers/AuthController.cs

```csharp
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpsDesk.Application.Auth.DTOs;
using OpsDesk.Application.Auth.Interfaces;

namespace OpsDesk.Api.Controllers;

[ApiController]
[Route("auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [AllowAnonymous]
    [HttpPost("register")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AuthResponse>> Register(
        RegisterRequest request,
        CancellationToken cancellationToken)
    {
        AuthResponse response =
            await _authService.RegisterAsync(
                request,
                cancellationToken);

        return StatusCode(
            StatusCodes.Status201Created,
            response);
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        AuthResponse response =
            await _authService.LoginAsync(
                request,
                cancellationToken);

        return Ok(response);
    }

    [Authorize]
    [HttpGet("/me")]
    public ActionResult<MeResponse> Me()
    {
        string? userIdClaim =
            User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(userIdClaim, out Guid userId))
        {
            return Unauthorized();
        }

        var response = new MeResponse(
            userId,
            User.FindFirstValue(ClaimTypes.Name) ?? string.Empty,
            User.FindFirstValue(JwtRegisteredClaimNames.Email)
                ?? string.Empty,
            User.FindFirstValue(ClaimTypes.Role) ?? string.Empty);

        return Ok(response);
    }
}
```

### C:/Users/Hamza/Documents/OpsDesk API/src/OpsDesk.Api/Program.cs

```csharp
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using OpsDesk.Api.Authorization;
using OpsDesk.Api.Configuration;
using OpsDesk.Api.RateLimiting;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using OpsDesk.Application.Agents.Interfaces;
using OpsDesk.Application.Agents.Services;
using OpsDesk.Application.Auth.Interfaces;
using OpsDesk.Application.Auth.Services;
using OpsDesk.Application.Tickets.Interfaces;
using OpsDesk.Application.Tickets.Services;
using OpsDesk.Api.ErrorHandling;
using OpsDesk.Infrastructure;
using OpsDesk.Infrastructure.Seed;
using OpsDesk.Infrastructure.Authentication;
using OpsDesk.Infrastructure.Persistence;
using OpsDesk.Application.Authorization;
using OpsDesk.Domain.Enums;
using Microsoft.Extensions.Options;
using Swashbuckle.AspNetCore.SwaggerGen;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRegistrationConfiguration();

builder.Services.AddScoped<OpsDesk.Application.Invitations.Interfaces.IInvitationService,
    OpsDesk.Application.Invitations.Services.InvitationService>();


builder.Services.AddHealthChecks();
builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(
            new JsonStringEnumConverter(
                JsonNamingPolicy.SnakeCaseLower,
                allowIntegerValues: false));
    });
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSingleton<TimeProvider>(
    TimeProvider.System);
builder.Services.AddScoped<IAgentService, AgentService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IPasswordRecoveryService, PasswordRecoveryService>();
builder.Services.AddScoped<JwtSessionValidationEvents>();
builder.Services.AddHostedService<OpsDesk.Api.BackgroundServices.PasswordRecoveryWorker>();
builder.Services.AddPasswordRecoveryRateLimiting();
builder.Services.AddScoped<
    IEmailVerificationService,
    EmailVerificationService>();
builder.Services.AddScoped<ITicketService, TicketService>();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();


builder.Services.AddSingleton<IPasswordValidator, PasswordValidator>();
builder.Services.AddSingleton<IEmailValidator, EmailValidator>();

builder.Services
    .AddAuthentication(
        JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();

builder.Services
    .AddOptions<JwtBearerOptions>(
        JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtSettings>>(
        (options, jwtOptions) =>
        {
            JwtSettings jwtSettings = jwtOptions.Value;

            options.MapInboundClaims = false;
            options.EventsType = typeof(JwtSessionValidationEvents);

            options.TokenValidationParameters =
                new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtSettings.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtSettings.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey =
                        new SymmetricSecurityKey(
                            Encoding.UTF8.GetBytes(
                                jwtSettings.SecretKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero,
                    NameClaimType = ClaimTypes.Name,
                    RoleClaimType = ClaimTypes.Role
                };
        });

builder.Services
    .AddAuthorizationBuilder()
    .AddPolicy(
        AuthorizationPolicies.VerifiedEmail,
        policy => policy.RequireAuthenticatedUser()
            .AddRequirements(new VerifiedEmailRequirement()))
    .AddPolicy(
        AuthorizationPolicies.AdminOnly,
        policy => policy.RequireRole(
            UserRole.Admin.ToString()))
    .AddPolicy(
        AuthorizationPolicies.AgentOrAdmin,
        policy => policy.RequireRole(
            UserRole.Admin.ToString(),
            UserRole.Agent.ToString()));

builder.Services.AddScoped<IAuthorizationHandler, VerifiedEmailHandler>();

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "OpsDesk API",
        Version = "v1",
        Description = "Internal support and operations management API."
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Enter the JWT access token."
    });

    options.AddSecurityRequirement(document =>
        new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(
                "Bearer",
                document)] = []
        });

    options.OperationFilter<TicketListExamplesOperationFilter>();
});

var app = builder.Build();

// Validate registration before startup database preparation, not only when the first request arrives.
_ = app.Services.GetRequiredService<IOptions<OpsDesk.Application.Auth.Models.RegistrationSettings>>().Value;

using (IServiceScope scope = app.Services.CreateScope())
{
    OpsDeskDbContext dbContext = scope.ServiceProvider
        .GetRequiredService<OpsDeskDbContext>();

    if (app.Environment.IsDevelopment() ||
        app.Environment.IsEnvironment("Testing"))
    {
        await dbContext.Database.MigrateAsync();
    }

    AdminUserSeeder adminUserSeeder =
        scope.ServiceProvider
            .GetRequiredService<AdminUserSeeder>();

    await adminUserSeeder.SeedAsync();
}

app.UseExceptionHandler();

app.UseSwagger();

app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint(
        "/swagger/v1/swagger.json",
        "OpsDesk API v1");

    options.DocumentTitle = "OpsDesk API";
    options.EnablePersistAuthorization();
});

app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapGet("/", () => Results.Redirect("/swagger"))
    .ExcludeFromDescription();

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";

        var response = new
        {
            status = report.Status.ToString(),
            service = "OpsDesk API"
        };

        await context.Response.WriteAsync(
            JsonSerializer.Serialize(response));
    }
})
    .WithName("Health")
    .WithSummary("Returns API health status.")
    .WithDescription(
        "Confirms that the API process is running.");

app.Run();

public partial class Program;

internal sealed class TicketListExamplesOperationFilter :
    IOperationFilter
{
    /// <summary>
    /// Adds practical query examples only to the Ticket collection operation.
    /// </summary>
    public void Apply(
        OpenApiOperation operation,
        OperationFilterContext context)
    {
        bool isTicketListOperation =
            string.Equals(
                context.ApiDescription.HttpMethod,
                HttpMethods.Get,
                StringComparison.OrdinalIgnoreCase) &&
            string.Equals(
                context.ApiDescription.RelativePath,
                "tickets",
                StringComparison.OrdinalIgnoreCase);

        if (!isTicketListOperation)
        {
            return;
        }

        operation.Description = """
            Example queries:

            GET /tickets?page=1&pageSize=20

            GET /tickets?status=open&priority=high

            GET /tickets?unassigned=true
            """;
    }
}
```

### C:/Users/Hamza/Documents/OpsDesk API/src/OpsDesk.Api/appsettings.json

```json
{
  "Registration": {
    "PublicRegistrationEnabled": false
  },
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=opsdesk;Username=opsdesk"
  },
  "Jwt": {
    "Issuer": "OpsDesk.Api",
    "Audience": "OpsDesk.Client",
    "ExpirationMinutes": 60
  },
  "Email": {
    "Host": "localhost",
    "Port": 1025,
    "UseSsl": false,
    "FromAddress": "noreply@opsdesk.local",
    "FromName": "OpsDesk",
    "VerificationUrl": "http://localhost:5044/auth/email-verification/confirm"
  },
  "PasswordRecovery": {
    "WorkerEnabled": true,
    "RateLimits": {
      "RequestPermitLimit": 5,
      "ResetPermitLimit": 10,
      "WindowMinutes": 15
    }
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

### C:/Users/Hamza/Documents/OpsDesk API/src/OpsDesk.Api/appsettings.Development.json

```json
{
  "Registration": {
    "PublicRegistrationEnabled": true
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

### C:/Users/Hamza/Documents/OpsDesk API/tests/OpsDesk.Tests/Integration/OpsDeskApiFactory.cs

```csharp
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpsDesk.Infrastructure.Persistence;
using OpsDesk.Application.Auth.Interfaces;
using OpsDesk.Application.Auth.Models;
using System.Collections.Concurrent;
using OpsDesk.Application.Invitations.Interfaces;
using OpsDesk.Application.Invitations.Models;

namespace OpsDesk.Tests.Integration;

public sealed class OpsDeskApiFactory :
    WebApplicationFactory<Program>
{
    public const string AdminEmail =
        "admin@opsdesk.test";

    public const string AdminPassword =
        "AdminTest!";

    private readonly string _connectionString;

    public ConcurrentQueue<EmailVerificationEmail> SentEmails { get; } = new();
    public ConcurrentQueue<InvitationEmail> SentInvitations { get; } = new();
    public PasswordResetMailbox PasswordResetEmails { get; } = new();

    // Prepares a verified account for ticket tests; auth tests use the real confirmation flow.
    public void VerifyAccount(string accessToken)
    {
        var jwt = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler()
            .ReadJwtToken(accessToken);
        Guid id = Guid.Parse(jwt.Claims.Single(claim =>
            claim.Type == System.Security.Claims.ClaimTypes.NameIdentifier).Value);
        using IServiceScope scope = Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<OpsDeskDbContext>();
        var user = database.Users.Single(user => user.Id == id);
        if (!user.IsEmailVerified)
        {
            user.MarkEmailVerified(DateTime.UtcNow);
            database.SaveChanges();
        }
    }

    public OpsDeskApiFactory(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            connectionString);

        _connectionString = connectionString;
    }

    protected override void ConfigureWebHost(
        IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration(
            (_, configuration) =>
            {
                configuration.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:DefaultConnection"] =
                            _connectionString,
                        ["Jwt:Issuer"] = "OpsDesk.Tests",
                        ["Jwt:Audience"] = "OpsDesk.Tests",
                        ["Jwt:SecretKey"] =
                            "TestSecretKeyForOpsDeskApi1234567890!",
                        ["Jwt:ExpirationMinutes"] = "60",
                        ["PasswordRecovery:WorkerEnabled"] = "false",
                        ["Registration:PublicRegistrationEnabled"] = "true",
                        ["AdminSeed:Enabled"] = "true",
                        ["AdminSeed:FirstName"] = "Test",
                        ["AdminSeed:LastName"] = "Administrator",
                        ["AdminSeed:Email"] = AdminEmail,
                        ["AdminSeed:Password"] = AdminPassword
                    });
            });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IPasswordResetEmailSender>();
            services.AddSingleton<IPasswordResetEmailSender>(PasswordResetEmails);
            services.RemoveAll<IInvitationEmailSender>();
            services.AddSingleton<IInvitationEmailSender>(new RecordingInvitationSender(SentInvitations));
            services.RemoveAll<IEmailVerificationEmailSender>();
            services.AddSingleton<IEmailVerificationEmailSender>(
                new RecordingEmailSender(SentEmails));
            services.RemoveAll<OpsDeskDbContext>();

            services.RemoveAll<
                DbContextOptions<OpsDeskDbContext>>();

            services.RemoveAll<
                IDbContextOptionsConfiguration<
                    OpsDeskDbContext>>();

            services.AddDbContext<OpsDeskDbContext>(
                options => options.UseNpgsql(
                    _connectionString));
        });
    }

    private sealed class RecordingEmailSender(
        ConcurrentQueue<EmailVerificationEmail> sentEmails)
        : IEmailVerificationEmailSender
    {
        // Captures outgoing email so HTTP tests can inspect delivery without SMTP.
        public Task SendAsync(
            EmailVerificationEmail email,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            sentEmails.Enqueue(email);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingInvitationSender(ConcurrentQueue<InvitationEmail> sentEmails)
        : IInvitationEmailSender
    {
        // Captures invitation delivery at the email adapter boundary for HTTP tests.
        public Task SendAsync(InvitationEmail email, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            sentEmails.Enqueue(email);
            return Task.CompletedTask;
        }
    }
}
```

### C:/Users/Hamza/Documents/OpsDesk API/tests/OpsDesk.Tests/Integration/RegistrationPolicyIntegrationTests.cs

```csharp
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OpsDesk.Application.Agents.DTOs;
using OpsDesk.Application.Auth.DTOs;
using OpsDesk.Application.Auth.Interfaces;
using OpsDesk.Application.Common.Exceptions;

namespace OpsDesk.Tests.Integration;

[Collection(IntegrationTestCollection.Name)]
public sealed class RegistrationPolicyIntegrationTests(OpsDeskApiFixture fixture)
{
    // A malformed boolean must fail binding at startup instead of being silently accepted as a policy.
    [Fact]
    public void Invalid_registration_setting_should_fail_startup()
    {
        using var factory = fixture.Factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["Registration:PublicRegistrationEnabled"] = "not-a-boolean" })));
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
        {
            using HttpClient client = factory.CreateClient();
        });
        Assert.Contains("Registration:PublicRegistrationEnabled", error.Message);
    }

    // Closing signup does not strand accounts waiting to use an already delivered verification token.
    [Fact]
    public async Task Disabled_registration_should_preserve_existing_email_confirmation()
    {
        string email = $"pending-verification-{Guid.NewGuid():N}@example.com";
        using HttpClient setup = fixture.Factory.CreateClient();
        using HttpResponseMessage registration = await setup.PostAsJsonAsync("/auth/register",
            new RegisterRequest("Pending", "Customer", email, "ValidPass!"));
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
        AuthResponse account = Assert.IsType<AuthResponse>(await registration.Content.ReadFromJsonAsync<AuthResponse>());
        var verification = Assert.Single(fixture.Factory.SentEmails, message => message.RecipientEmail == email);
        using var factory = fixture.Factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["Registration:PublicRegistrationEnabled"] = "false" })));
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage confirmed = await client.PostAsJsonAsync("/auth/email-verification/confirm",
            new ConfirmEmailRequest(verification.RawToken));
        Assert.Equal(HttpStatusCode.NoContent, confirmed.StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", account.AccessToken);
        using HttpResponseMessage tickets = await client.GetAsync("/tickets");
        Assert.Equal(HttpStatusCode.OK, tickets.StatusCode);
    }

    // Environment names do not implicitly grant registration; only explicit non-production enablement does.
    [Theory]
    [InlineData("Development", "true", HttpStatusCode.Created)]
    [InlineData("Demo", "true", HttpStatusCode.Created)]
    [InlineData("Development", "false", HttpStatusCode.Forbidden)]
    [InlineData("Production", "false", HttpStatusCode.Forbidden)]
    [InlineData("Production", null, HttpStatusCode.Forbidden)]
    [InlineData("Staging", null, HttpStatusCode.Forbidden)]
    public async Task Environment_configuration_should_control_public_registration(
        string environment, string? enabled, HttpStatusCode expected)
    {
        // Initialize the disposable schema through the normal Testing host; Production never migrates it.
        using HttpClient setup = fixture.Factory.CreateClient();
        using var factory = fixture.Factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Registration:PublicRegistrationEnabled"] = enabled,
                    ["AdminSeed:Enabled"] = "false"
                }));
        });
        using HttpClient client = factory.CreateClient();
        Assert.Equal(environment, factory.Services.GetRequiredService<IHostEnvironment>().EnvironmentName);
        string email = $"environment-{Guid.NewGuid():N}@example.com";
        using HttpResponseMessage response = await client.PostAsJsonAsync("/auth/register",
            new RegisterRequest("Environment", "Visitor", email, "ValidPass!"));
        Assert.Equal(expected, response.StatusCode);
        using HttpResponseMessage login = await client.PostAsJsonAsync("/auth/login", new LoginRequest(email, "ValidPass!"));
        Assert.Equal(expected == HttpStatusCode.Created ? HttpStatusCode.OK : HttpStatusCode.Unauthorized, login.StatusCode);
        if (expected == HttpStatusCode.Created)
        {
            Assert.Single(fixture.Factory.SentEmails, message => message.RecipientEmail == email);
        }
        else
        {
            Assert.DoesNotContain(fixture.Factory.SentEmails, message => message.RecipientEmail == email);
        }
    }

    // Guards the public Application interface as well as HTTP, without mocking its dependencies.
    [Fact]
    public async Task Disabled_registration_should_also_reject_direct_service_calls()
    {
        using var factory = fixture.Factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["Registration:PublicRegistrationEnabled"] = "false" })));
        using HttpClient client = factory.CreateClient();
        using IServiceScope scope = factory.Services.CreateScope();
        IAuthService auth = scope.ServiceProvider.GetRequiredService<IAuthService>();
        string email = $"direct-disabled-{Guid.NewGuid():N}@example.com";
        await Assert.ThrowsAsync<ForbiddenException>(() => auth.RegisterAsync(
            new RegisterRequest("Blocked", "Visitor", email, "ValidPass!")));
        using HttpResponseMessage login = await client.PostAsJsonAsync("/auth/login", new LoginRequest(email, "ValidPass!"));
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    // Invitation onboarding and password recovery remain available on a closed Production registration host.
    [Fact]
    public async Task Disabled_registration_should_preserve_invitation_login_and_password_recovery()
    {
        using HttpClient setup = fixture.Factory.CreateClient();
        using var factory = fixture.Factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Registration:PublicRegistrationEnabled"] = "false",
                    ["AdminSeed:Enabled"] = "false",
                    ["PasswordRecovery:WorkerEnabled"] = "true"
                }));
        });
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage adminLogin = await client.PostAsJsonAsync("/auth/login",
            new LoginRequest(OpsDeskApiFactory.AdminEmail, OpsDeskApiFactory.AdminPassword));
        Assert.Equal(HttpStatusCode.OK, adminLogin.StatusCode);
        AuthResponse admin = Assert.IsType<AuthResponse>(await adminLogin.Content.ReadFromJsonAsync<AuthResponse>());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", admin.AccessToken);
        string email = $"closed-invite-{Guid.NewGuid():N}@example.com";
        using HttpResponseMessage invite = await client.PostAsJsonAsync("/admin/invitations", new { email, role = "customer" });
        Assert.Equal(HttpStatusCode.Created, invite.StatusCode);
        var delivered = Assert.Single(fixture.Factory.SentInvitations, message => message.RecipientEmail == email);
        client.DefaultRequestHeaders.Authorization = null;
        using HttpResponseMessage accepted = await client.PostAsJsonAsync("/auth/invitations/accept",
            new { token = delivered.RawToken, firstName = "Invited", lastName = "Customer", password = "ValidPass!" });
        Assert.Equal(HttpStatusCode.Created, accepted.StatusCode);
        using HttpResponseMessage login = await client.PostAsJsonAsync("/auth/login", new LoginRequest(email, "ValidPass!"));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        using HttpResponseMessage forgot = await client.PostAsJsonAsync("/auth/forgot-password", new { email });
        Assert.Equal(HttpStatusCode.Accepted, forgot.StatusCode);
        var recovery = await fixture.Factory.PasswordResetEmails.WaitForAsync(email);
        using HttpResponseMessage reset = await client.PostAsJsonAsync("/auth/reset-password", new ResetPasswordRequest(recovery.RawToken, "ChangedPass!"));
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        using HttpResponseMessage newLogin = await client.PostAsJsonAsync("/auth/login", new LoginRequest(email, "ChangedPass!"));
        Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);
        AuthResponse recovered = Assert.IsType<AuthResponse>(await newLogin.Content.ReadFromJsonAsync<AuthResponse>());
        Assert.Equal("Customer", recovered.Role);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", recovered.AccessToken);
        using HttpResponseMessage tickets = await client.GetAsync("/tickets");
        Assert.Equal(HttpStatusCode.OK, tickets.StatusCode);
    }

    // Closing self-registration does not remove the administrator's existing Agent provisioning capability.
    [Fact]
    public async Task Disabled_registration_should_preserve_admin_agent_provisioning()
    {
        using var factory = fixture.Factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["Registration:PublicRegistrationEnabled"] = "false" })));
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage login = await client.PostAsJsonAsync("/auth/login",
            new LoginRequest(OpsDeskApiFactory.AdminEmail, OpsDeskApiFactory.AdminPassword));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        AuthResponse admin = Assert.IsType<AuthResponse>(await login.Content.ReadFromJsonAsync<AuthResponse>());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", admin.AccessToken);
        string email = $"closed-agent-{Guid.NewGuid():N}@example.com";
        using HttpResponseMessage created = await client.PostAsJsonAsync("/admin/agents",
            new CreateAgentRequest("Created", "Agent", email, "ValidPass!"));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        client.DefaultRequestHeaders.Authorization = null;
        using HttpResponseMessage agentLogin = await client.PostAsJsonAsync("/auth/login", new LoginRequest(email, "ValidPass!"));
        Assert.Equal(HttpStatusCode.OK, agentLogin.StatusCode);
        AuthResponse agent = Assert.IsType<AuthResponse>(await agentLogin.Content.ReadFromJsonAsync<AuthResponse>());
        Assert.Equal("Agent", agent.Role);
    }

    // Production must refuse an explicit enablement instead of starting with public signup exposed.
    [Fact]
    public void Production_with_enabled_registration_should_fail_startup()
    {
        using var factory = fixture.Factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Registration:PublicRegistrationEnabled"] = "true",
                    ["AdminSeed:Enabled"] = "false"
                }));
        });
        OptionsValidationException error = Assert.Throws<OptionsValidationException>(() =>
        {
            using HttpClient client = factory.CreateClient();
        });
        Assert.Contains("Production", error.Message);
        Assert.Contains("Registration:PublicRegistrationEnabled", error.Message);
    }

    // A forbidden registration must not leave an account that can subsequently log in.
    [Fact]
    public async Task Disabled_registration_should_reject_the_request_without_creating_an_account()
    {
        using var factory = fixture.Factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["Registration:PublicRegistrationEnabled"] = "false" })));
        using HttpClient client = factory.CreateClient();
        string email = $"registration-disabled-{Guid.NewGuid():N}@example.com";
        using HttpResponseMessage response = await client.PostAsJsonAsync("/auth/register",
            new RegisterRequest("Blocked", "Visitor", email, "ValidPass!"));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        using HttpResponseMessage login = await client.PostAsJsonAsync("/auth/login", new LoginRequest(email, "ValidPass!"));
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
        Assert.DoesNotContain(fixture.Factory.SentEmails, message => message.RecipientEmail == email);
    }
}
```
