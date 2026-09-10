# AUTH-006: Kurtarma Talebi ve Arka Plan E-postasi

2026-09-06. Bu belge ilk dikey dilimin inceleme kopyasidir; guncel kaynak src/ ve tests/ dosyalaridir.
Dogrulama: 276 test basarili, 0 basarisiz, 0 atlanan. Build: 0 uyari/hata. Slopwatch: 0 bulgu.

GUNCELLEME 2026-09-07: reset endpoint'i, tek kullanim ve AuthVersion ile eski JWT iptali tamamlandi; AUTH-006 Review. Guncel aciklama ve tam kaynaklar: [Parola sifirlama dilimi](auth-006-password-reset.md). Asagidaki HENUZ YOK ifadeleri yalnizca ilk dilimin tarihsel durumudur.

## Ilk Dilim Tarihindeki Durum

POST /auth/forgot-password email alir ve bilinen/bilinmeyen adresler icin ayni 202 cevabini doner.
202, e-postanin teslim edildigi degil talebin PostgreSQL kuyruguna kaydedildigi anlamina gelir.
Bicimi hatali email 400, IP siniri asimi 429 doner. Varsayilan IP siniri tek uygulama instance'inda 15 dakikada 5 taleptir.
Arka plan isi mevcut parola tabanli hesaba 30 dakikalik pwd_ tokeni gonderir. Dogrulanmamis hesap da kurtarma isteyebilir; dogrulama durumu DEGISMEZ.
Ayni hesap icin 60 saniyede en fazla bir token uretme/gonderme girisimi yapilir. Yeni token oncekini degistirir; DB'de sadece hash vardir.

HENUZ YOK: POST /auth/reset-password, yeni parolayi kaydetme, tek kullanimlik tuketme, AuthVersion ve eski JWT iptali. AUTH-006 In Progress kalir.
Bu token JWT degildir, Swagger Authorize alanina yazilmaz. Reset endpoint'i sonraki dilimdedir.
Tarayici formu yok; e-postada token metni vardir. Gelistirmede noreply@opsdesk.local adresinden Mailpit'e gider, dis posta kutusuna teslim iddiasi yoktur.
Commit/push/tag ve kalici gelistirme DB'sine database update yapilmadi. Test DB'lerine iki yeni migration uygulandi.

## Akis ve Katmanlar

HTTP -> PasswordRecoveryController.ForgotPassword -> PasswordRecoveryService.RequestAsync -> PasswordRecoveryQueue.EnqueueAsync -> PostgreSQL -> 202.

Bu yolda hesap var mi sorgusu veya SMTP yoktur. Bilinmeyen adres de ayni INSERT yolundan gecer.
SMTP suresi ve hatasi HTTP cevabini belirlemez; bu, her istegin milisaniye cinsinden birebir esit donecegi garantisi degildir.

Worker -> queue.ClaimAsync -> service.SendAsync -> User lookup -> token repository -> SMTP -> queue.CompleteAsync.

Domain is kurallarini, Application is akislarini, Infrastructure PostgreSQL/SMTP ayrintilarini, Api HTTP ve uygulama yasam dongusunu tutar.
PasswordRecoveryJob teknik bir kuyruk kaydidir; Infrastructure/Persistence/Entities altinda olmasinin nedeni budur.
PasswordResetToken ise hesabi kurtarmaya yarayan is kavramidir; Domain/Entities altindadir.

## Yeni Dosyalarin Yeri

Her dosyanin TAM yolu ve TAM icerigi belgenin sonundaki basliklarda bulunur.
- Application/Auth/DTOs: ForgotPasswordRequest, istemcinin gonderebildigi tek email alanini sinirlar.
- Application/Auth/Interfaces: IPasswordRecoveryService, IPasswordRecoveryQueue, IPasswordResetTokenRepository, IPasswordResetTokenGenerator, IPasswordResetEmailSender.
- Application/Auth/Models: PasswordRecoveryWorkItem (kuyruk isi), GeneratedPasswordResetToken (ham token/hash), PasswordResetEmail (gonderim verisi).
- Application/Auth/Services: PasswordRecoveryService, talep/teslim siralamasini kurar.
- Domain/Entities: PasswordResetToken, tokenin kimlik ve gecerlilik bilgisi.
- Infrastructure/Authentication: PasswordResetTokenGenerator, kriptografik token uygulamasi.
- Infrastructure/Email: SmtpPasswordResetEmailSender; mevcut SmtpEmailTransport da duzeltildi.
- Infrastructure/Persistence/Entities: PasswordRecoveryJob, yeni teknik kuyruk klasoru.
- Infrastructure/Persistence/Configurations: PasswordRecoveryJobConfiguration ve PasswordResetTokenConfiguration.
- Infrastructure/Persistence/Repositories: PasswordRecoveryQueue ve PasswordResetTokenRepository.
- Api/BackgroundServices: PasswordRecoveryWorker; uygulama arka plan isleri icin yeni klasor.
- Api/RateLimiting: PasswordRecoveryRateLimiting; HTTP sinirlarini ayiran yeni klasor.
- Api/Controllers: PasswordRecoveryController.
- Tests/Integration: PasswordRecoveryIntegrationTests ve PasswordResetMailbox.
- Tests/Email: SmtpTransportLifecycleTests.

Program.cs servis/worker/limiter kayitlarini, appsettings.json sinir/worker ayarlarini, DbContext yeni DbSet'leri saglar.
Infrastructure/DependencyInjection.cs repository, generator ve sender'i DI'ye kaydeder.
OpsDeskApiFactory worker'i varsayilan olarak kapali tutar; teslim testleri gercek worker'i acar. SMTP yerine kayit alan mailbox kullanilir.
Ayrica gercek Mailpit SMTP testi vardir; butun e-posta testleri fake degildir.

## C# Imza Kilavuzu

public: diger siniflar cagirabilir. private: sinifin ic yardimcisi. static: nesne olusturmadan cagrilir.
protected override: temel sinifin sagladigi metodu bu sinifa ozel uygular; worker ExecuteAsync buna ornektir.
async: await iceren metot. await, I/O islemini thread'i bosuna bekletmeden bekler; otomatik yeni thread acmaz.
Task: ileride bitecek is, sonuc verisi yok. Task<T>: bitince T tipi sonuc. T donus tipidir; metodun parametresi degildir.
Task<PasswordRecoveryWorkItem?> bir is veya null DONDURUR. Guid? ve DateTime? de null olabilen degerlerdir.
Task<IActionResult> asenkron HTTP cevabidir. Accepted 202 cevabi uretir.
record, DTO/model verisi tasimak icin C# turudur. sealed turetilmesini engeller.
CancellationToken iptal/kapanis/sure sinirini alt islemlere bildirir; default ozel iptal bilgisi verilmedigi anlamina gelir.
using kaynaklari kapsam sonunda temizler; await using asenkron temizligi de bekler.

## Metotlar Tek Tek

### Controller.ForgotPassword
public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request, CancellationToken cancellationToken).
request HTTP JSON govdesinden, cancellationToken HTTP isteginden gelir. Service.RequestAsync beklenir ve sabit mesajli 202 doner.
Neden Api? HTTP status ve endpoint ozellikleri buraya aittir; parola/hash/SQL burada degildir.

### Service.RequestAsync
public Task RequestAsync(string email, CancellationToken cancellationToken = default).
EmailValidator -> UserInputNormalizer trim/lowercase -> TimeProvider UTC -> queue.EnqueueAsync. Kullanici sorgusu yapmaz.
Task dogrudan geri verildiginden async kelimesi gerekli degildir. Kayit tamamlanmadan controller 202 donmez.
Neden Application? Is adimlarinin sirasi buradadir, SQL Infrastructure'dadir.

### Service.SendAsync
public async Task SendAsync(string email, DateTime requestedAtUtc, CancellationToken cancellationToken = default).
email/tarih kuyruktan gelir; controller tarafindan HTTP endpoint'i olarak acilmaz.
30 dakikadan eski/gelecekteki isi atlar. Hesap talep aninda yoksa veya parola hash'i yoksa email gondermez.
Generator -> Domain.Create -> repository.TryReplaceAsync. false sonucu bekleme suresinin dolmadigini belirtir, gonderim atlanir.
true ise sender.SendAsync cagrilir. Hatada o token RevokeAsync ile iptal edilir; exception worker'a iletilir.
Temizlik iptali teslimin suresi dolmus tokenine baglanmaz; ayri 5 saniye siniri vardir.
Yan etki token kaydi ve postadir. Kullanici parolasi, rol ve email dogrulamasi DEGISMEZ.

### PasswordResetToken.Create
public static PasswordResetToken Create(Guid userId, string tokenHash, DateTime createdAtUtc).
UserId DB'deki hesaptan, hash generator'dan, tarih TimeProvider'dan gelir. Bos Guid, yanlis hash uzunlugu ve UTC olmayan tarih reddedilir.
Yeni Guid ve 30 dakikalik ExpiresAtUtc ile ENTITY doner; burada DB'ye kayit yapilmaz. Bu nedenle Domain'dedir.

### Generator.GenerateToken
public GeneratedPasswordResetToken GenerateToken(). Parametre yoktur.
Mevcut kriptografik generator 256-bit rastgele veri uretir; pwd_ on eki amaci ayirir.
Ham token email modeline, SHA-256 hash DB'ye gider. Teknoloji ayrintisi oldugu icin Infrastructure'dadir.

### Queue.EnqueueAsync
public async Task EnqueueAsync(string email, DateTime requestedAtUtc, CancellationToken cancellationToken = default).
Yeni job ekler; RequestedAtUtc ve AvailableAtUtc ilk basta aynidir. SaveChangesAsync ile kalici hale getirir.
DB basarisizsa 202 ile sahte basari verilmez. Burada parola veya ham token saklanmaz.

### Queue.ClaimAsync
public async Task<PasswordRecoveryWorkItem?> ClaimAsync(DateTime nowUtc, CancellationToken cancellationToken = default).
Worker UTC zamani gonderir; en eski uygun isi kisa transaction ile alir.
FOR UPDATE SKIP LOCKED, baska worker'in kilitledigi isi beklemeden diger uygun isi secer.
LeaseId sahiplik kimligi olur; AvailableAtUtc iki dakika ileri alinir; Attempts artar. Commit yapilip kilit birakilir.
Is yoksa null; varsa Id/Email/RequestedAtUtc/LeaseId/Attempts kaydi doner. SMTP boyunca DB kilidi tutulmaz.

### Queue.CompleteAsync
public Task CompleteAsync(PasswordRecoveryWorkItem job, CancellationToken cancellationToken = default).
Isin Id ve LeaseId'si birlikte eslesirse siler. Eski worker, baskasinin yeniden sahiplendigi isi silemez.
Task sonuc verisi tasimaz; EF'in satir sayisi bu arayuzun ciktisi degildir.

### Queue.RetryAsync
public Task RetryAsync(PasswordRecoveryWorkItem job, DateTime nowUtc, CancellationToken cancellationToken = default).
Ucuncu denemeden sonra siler; onceki denemelerde lease'i birakir, 60 saniye sonrasina erteler.
Bu davranis DB'de kalicidir; uygulama yeniden baslasa da tekrar deneme zamani korunur.

### TokenRepository.TryReplaceAsync
public async Task<bool> TryReplaceAsync(PasswordResetToken token, CancellationToken cancellationToken = default).
Token satiri henuz yokken de guvenli olsun diye User satirini FOR UPDATE ile kilitler. Bu User'i degistirmez.
Son olusma tarihi 60 saniyeden yeniyse false; degilse eski token silinir, yenisi transaction icinde kaydedilir ve true doner.
SQL ve EF transaction'i Infrastructure'dadir; Application sadece bu sozlesmeyi bilir.

### TokenRepository.RevokeAsync
public Task RevokeAsync(Guid tokenId, DateTime revokedAtUtc, CancellationToken cancellationToken = default).
Yalnizca verilen tokenin iptal tarihini SQL UPDATE ile yazar; daha yeni tokenin Id'si farkli oldugundan o etkilenmez.
Olusma zamani korunur; SMTP hatasi gonderim bekleme suresini kaldirmaz.

### SmtpPasswordResetEmailSender.SendAsync
public async Task SendAsync(PasswordResetEmail email, CancellationToken cancellationToken = default).
Model alici/token/bitis tarihini tasir; parola icermez. MimeMessage uretip ortak transport'a verir.
IOptions<EmailSettings>, DI'nin verdigi tipli ayarlardir; Value ayar nesnesidir. Infrastructure'dadir cunku SMTP bir teknolojidir.

### SmtpEmailTransport.SendAsync
internal static async Task SendAsync(EmailSettings settings, MimeMessage message, CancellationToken cancellationToken).
ConnectAsync -> SendAsync -> using ile Dispose. internal yalnizca Infrastructure assembly'sine aciktir.
Eski kod QUIT cevabini CancellationToken.None ile bekliyordu. SMTP mesaj kabulunden sonra bu bekleme takilabiliyordu.
Testte timeout ile yeniden uretildi; gereksiz ek QUIT alisverisi kaldirildi. SendAsync sonucu teslim sonucudur.
SMTP kabulu, son alicinin postayi okudugu veya dis posta kutusuna kesin ulastigi anlamina gelmez.

### Worker.ExecuteAsync
protected override async Task ExecuteAsync(CancellationToken stoppingToken).
.NET host baslatir; uygulama kapanirken token iptal edilir. WorkerEnabled false ise cikilir.
PeriodicTimer her saniye kuyrugu yoklar; tur basina en fazla 10 is sirali islenir.
DB hatasi guvenli hata turuyla loglanir ve 5 saniye geri cekilip tekrar denenir. Kapanis iptalinde return edilir.
Worker Api'dedir cunku host yasam dongusunu bilir; is kurallari yine Application'dadir.

### Worker.ProcessNextAsync
private async Task<bool> ProcessNextAsync(CancellationToken stoppingToken).
CreateAsyncScope her is icin yeni scoped servis/DbContext saglar; singleton worker eski DbContext'i yakalamaz.
Claim -> eski/fazla-denenmis isi sil -> SendAsync icin 30 saniyelik iptal -> Complete.
Gonderim hatasinda guvenli job Id/deneme/hata TURU loglanir; token/email payload'i veya exception mesaji loglanmaz.
RetryAsync sonraki denemeyi kaydeder. true bir is ele alindi, false uygun is yok demektir.
Cokme/kapanis sonrasinda iki dakikalik lease bitince is yeniden alinabilir. SMTP+DB atomik olmadigindan mukerrer posta mumkundur; exactly-once garantisi yoktur.

### RateLimiting.AddPasswordRecoveryRateLimiting
public static IServiceCollection AddPasswordRecoveryRateLimiting(this IServiceCollection services).
this extension method yazimidir; builder.Services.AddPasswordRecoveryRateLimiting() cagrilabilir.
IServiceCollection DI listesidir; geri dondurmek zincirleme kullanima izin verir.
IConfiguration DI'den son haliyle okunur; test/deployment override'larini erken okuyup kaybetme hatasi testle yakalanip duzeltildi.
FixedWindowRateLimiter IP basina pencere uygular. QueueLimit=0 fazladan istegi bekletmez, 429 doner.
Keyfi X-Forwarded-For basligi okunmaz. Reverse proxy guven ayari ve cok-instance ortak limiter sonraki deployment kararlaridir.
Reset endpoint'inin 10/15 dakika policy'si bu dilimde henuz eklenmedi.

### Configuration.Configure ve Migration
public void Configure(EntityTypeBuilder<T> builder), EF'in verdigi builder'a tablo/kolon/index kurallarini ekler; void sonuc yok demektir.
Queue, email/zaman/lease/deneme alanlarini tutar. Token tablosu User foreign key'i ve UserId/TokenHash unique index'lerini tutar.
Iki migration EF CLI ile uretildi. Up tablolari ekler, Down kaldirir; Down veri kaybina yol acabilecegi icin calistirilmadi.

## Testler ve Yardimcilari

Testler public async Task ve Fact'tir: xUnit cagirir, Assert tutmazsa basarisizdir.
- Forgot_password_should_accept_known_and_unknown_addresses_identically: ayni 202/govde, parola hala ayni.
- Recovery_request_should_deliver_a_thirty_minute_token_in_background: worker teslimi ve yanlis amac reddi.
- Forgot_password_should_enforce_configured_ip_limit: test limiti 2, ucuncu istek 429.
- Queued_request_should_survive_host_restart: worker kapali host kabul eder, yeni host teslim eder.
- Account_cooldown_should_suppress_duplicates_and_allow_delivery_after_sixty_seconds: kontrollu saat ve ikinci aliciya teslim edilen kuyruk bariyeri.
- Slow_delivery_should_not_delay_the_http_acknowledgement: sender bekletilir; HTTP yine 202 tamamlar.
- Failed_background_delivery_should_retry_without_changing_the_public_response: ilk gonderim hata, sonraki is tamamlanir, saat ilerler ve yeni tokenle yeniden teslim.
- Accepted_email_should_not_wait_for_a_quit_reply: gercek TCP test SMTP sunucusu DATA'yi kabul edip QUIT'e cevap vermez.
- SendAsync_should_deliver_password_reset_email_to_mailpit: gercek SMTP konu/alici/token kontrolu.

PasswordResetMailbox.SendAsync modeli ConcurrentQueue/Channel'a kaydeder; WaitForAsync(string recipient) 20 saniye son tarihli olay bekler, rastgele uyumaz.
RecoveryClock.GetUtcNow/Advance(TimeSpan amount), Interlocked ile worker ve test arasinda guvenli saat okuma/yazma yapar; sistem saatini degistirmez.
BlockingResetSender.SendAsync, secili alicida TaskCompletionSource kapisini bekler; test finally ile acar.
FailOnceResetSender.SendAsync, hedefin ilk denemesinde IOException firlatir, sonraki denemeyi normal mailbox'a verir.
RunServerAsync(TcpListener listener, CancellationToken), test SMTP komutlarini cevaplar; DATA sonunda 250 doner, QUIT'e cevap yoktur.

Ilk HTTP (404), ilk worker (teslim yok), IP limiter (202 yerine429) ve SMTP kapanis (timeout) testleri RED goruldu, duzeltmelerle GREEN oldu.
Digerleri regresyon kapsamini genisletti; her biri icin ayri production hatasi goruldugu iddia edilmiyor.
Uc denemede birakma ve tum coklu-worker/lease kombinasyonlari ayri testlerle zorlanmadi; kalan risk olarak not edildi.

## Skill ve Inceleme

tdd: HTTP davranis siniri ve red/green. codebase-design: is akisi ile teknik adapter'lari ayirma.
efcore-patterns: kisa transaction, NoTracking okuma, EF CLI migration.
dependency-injection-patterns: scoped DB'yi background worker'da yeni scope ile kullanma ve limiter extension kaydi.
diagnosing-bugs: limiter ayar hatasini dar HTTP testiyle takip etme. dotnet-slopwatch: 0 bulgu.
Ilk alt ajan limit nedeniyle tamamlanamadi; ikinci salt-okunur inceleme SMTP kapanis riskini buldu, test ve fix ile dogrulandi.

## Komutlar

Klasor: C:/Users/Hamza/Documents/OpsDesk API

```powershell
dotnet test OpsDesk.sln --no-restore
```
```powershell
dotnet build OpsDesk.sln --no-restore
```

```powershell
dotnet slopwatch analyze --fail-on warning
```

## Tam Kaynak Kopyalari

### C:/Users/Hamza/Documents/OpsDesk API/tests/OpsDesk.Tests/Integration/PasswordResetMailbox.cs

```csharp
using System.Threading.Channels;
using System.Collections.Concurrent;
using OpsDesk.Application.Auth.Interfaces;
using OpsDesk.Application.Auth.Models;

namespace OpsDesk.Tests.Integration;

public sealed class PasswordResetMailbox : IPasswordResetEmailSender
{
    private readonly Channel<PasswordResetEmail> _messages = Channel.CreateUnbounded<PasswordResetEmail>();
    public ConcurrentQueue<PasswordResetEmail> Delivered { get; } = new();

    // Captures real worker delivery at the email adapter instead of using arbitrary test sleeps.
    public Task SendAsync(PasswordResetEmail email, CancellationToken cancellationToken = default)
    {
        Delivered.Enqueue(email);
        return _messages.Writer.WriteAsync(email, cancellationToken).AsTask();
    }

    // Waits for the matching delivery with a failure deadline, not a fixed success delay.
    public async Task<PasswordResetEmail> WaitForAsync(string recipient)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await foreach (PasswordResetEmail email in _messages.Reader.ReadAllAsync(deadline.Token))
        {
            if (email.RecipientEmail == recipient) return email;
        }
        throw new InvalidOperationException("Mailbox closed before delivery.");
    }
}
```

### C:/Users/Hamza/Documents/OpsDesk API/tests/OpsDesk.Tests/Integration/PasswordRecoveryIntegrationTests.cs

```csharp
using System.Net;
using System.Net.Http.Json;
using OpsDesk.Application.Auth.DTOs;
using OpsDesk.Application.Auth.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpsDesk.Application.Auth.Interfaces;

namespace OpsDesk.Tests.Integration;

[Collection(IntegrationTestCollection.Name)]
public sealed class PasswordRecoveryIntegrationTests(OpsDeskApiFixture fixture)
{
    // A later successful job confirms failure handling has finished before the clock advances for retry.
    [Fact]
    public async Task Failed_background_delivery_should_retry_without_changing_the_public_response()
    {
        string email = $"failed-{Guid.NewGuid():N}@example.com";
        string barrier = $"after-failure-{Guid.NewGuid():N}@example.com";
        var clock = new RecoveryClock(DateTimeOffset.UtcNow.AddMinutes(1));
        var sender = new FailOnceResetSender(email, fixture.Factory.PasswordResetEmails);
        using var factory = fixture.Factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?> { ["PasswordRecovery:WorkerEnabled"] = "true" }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(clock);
                services.RemoveAll<IPasswordResetEmailSender>();
                services.AddSingleton<IPasswordResetEmailSender>(sender);
            });
        });
        using HttpClient client = factory.CreateClient();
        foreach (string address in new[] { email, barrier })
        {
            using HttpResponseMessage registration = await client.PostAsJsonAsync("/auth/register",
                new RegisterRequest("Recovery", "User", address, "ValidPass!"));
            Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
        }
        using HttpResponseMessage request = await client.PostAsJsonAsync("/auth/forgot-password", new { email });
        Assert.Equal(HttpStatusCode.Accepted, request.StatusCode);
        clock.Advance(TimeSpan.FromSeconds(1));
        using HttpResponseMessage barrierResponse = await client.PostAsJsonAsync("/auth/forgot-password", new { email = barrier });
        Assert.Equal(HttpStatusCode.Accepted, barrierResponse.StatusCode);
        await fixture.Factory.PasswordResetEmails.WaitForAsync(barrier);
        Assert.DoesNotContain(fixture.Factory.PasswordResetEmails.Delivered, message => message.RecipientEmail == email);
        Assert.NotNull(sender.FailedToken);
        clock.Advance(TimeSpan.FromSeconds(61));
        PasswordResetEmail retry = await fixture.Factory.PasswordResetEmails.WaitForAsync(email);
        Assert.NotEqual(sender.FailedToken, retry.RawToken);
    }

    private sealed class FailOnceResetSender(string recipient, PasswordResetMailbox mailbox) : IPasswordResetEmailSender
    {
        private int _attempts;
        public string? FailedToken { get; private set; }

        // Simulates one transport failure while using the same capture adapter for subsequent delivery.
        public Task SendAsync(PasswordResetEmail email, CancellationToken cancellationToken = default)
        {
            if (email.RecipientEmail == recipient && Interlocked.Increment(ref _attempts) == 1)
            {
                FailedToken = email.RawToken;
                throw new IOException("Simulated SMTP delivery failure.");
            }
            return mailbox.SendAsync(email, cancellationToken);
        }
    }

    // A new host resumes a request that was accepted while its delivery worker was disabled.
    [Fact]
    public async Task Queued_request_should_survive_host_restart()
    {
        string email = $"durable-{Guid.NewGuid():N}@example.com";
        using (var firstFactory = fixture.Factory.WithWebHostBuilder(_ => { }))
        using (HttpClient client = firstFactory.CreateClient())
        {
            using HttpResponseMessage registered = await client.PostAsJsonAsync("/auth/register",
                new RegisterRequest("Recovery", "User", email, "ValidPass!"));
            Assert.Equal(HttpStatusCode.Created, registered.StatusCode);
            using HttpResponseMessage accepted = await client.PostAsJsonAsync("/auth/forgot-password", new { email });
            Assert.Equal(HttpStatusCode.Accepted, accepted.StatusCode);
        }
        using var restarted = fixture.Factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?> { ["PasswordRecovery:WorkerEnabled"] = "true" })));
        using HttpClient restartedClient = restarted.CreateClient();
        PasswordResetEmail delivered = await fixture.Factory.PasswordResetEmails.WaitForAsync(email);
        Assert.Equal(email, delivered.RecipientEmail);
    }

    // A later delivery acts as a queue barrier, so absence of a duplicate does not depend on sleeping.
    [Fact]
    public async Task Account_cooldown_should_suppress_duplicates_and_allow_delivery_after_sixty_seconds()
    {
        var clock = new RecoveryClock(DateTimeOffset.UtcNow.AddMinutes(1));
        using var factory = fixture.Factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?> { ["PasswordRecovery:WorkerEnabled"] = "true" }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(clock);
            });
        });
        using HttpClient client = factory.CreateClient();
        string email = $"cooldown-{Guid.NewGuid():N}@example.com";
        string barrier = $"barrier-{Guid.NewGuid():N}@example.com";
        foreach (string address in new[] { email, barrier })
        {
            using HttpResponseMessage registered = await client.PostAsJsonAsync("/auth/register",
                new RegisterRequest("Recovery", "User", address, "ValidPass!"));
            Assert.Equal(HttpStatusCode.Created, registered.StatusCode);
        }
        using HttpResponseMessage first = await client.PostAsJsonAsync("/auth/forgot-password", new { email });
        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        PasswordResetEmail firstEmail = await fixture.Factory.PasswordResetEmails.WaitForAsync(email);
        clock.Advance(TimeSpan.FromSeconds(10));
        using HttpResponseMessage duplicate = await client.PostAsJsonAsync("/auth/forgot-password",
            new { email = email.ToUpperInvariant() });
        Assert.Equal(HttpStatusCode.Accepted, duplicate.StatusCode);
        clock.Advance(TimeSpan.FromSeconds(1));
        using HttpResponseMessage barrierResponse = await client.PostAsJsonAsync("/auth/forgot-password", new { email = barrier });
        Assert.Equal(HttpStatusCode.Accepted, barrierResponse.StatusCode);
        await fixture.Factory.PasswordResetEmails.WaitForAsync(barrier);
        Assert.Single(fixture.Factory.PasswordResetEmails.Delivered, message => message.RecipientEmail == email);
        clock.Advance(TimeSpan.FromSeconds(49));
        using HttpResponseMessage retry = await client.PostAsJsonAsync("/auth/forgot-password", new { email });
        Assert.Equal(HttpStatusCode.Accepted, retry.StatusCode);
        PasswordResetEmail replacement = await fixture.Factory.PasswordResetEmails.WaitForAsync(email);
        Assert.NotEqual(firstEmail.RawToken, replacement.RawToken);
    }

    // Even a sender that has not completed cannot hold the public recovery response open.
    [Fact]
    public async Task Slow_delivery_should_not_delay_the_http_acknowledgement()
    {
        string email = $"slow-{Guid.NewGuid():N}@example.com";
        var sender = new BlockingResetSender(email);
        using var factory = fixture.Factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?> { ["PasswordRecovery:WorkerEnabled"] = "true" }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IPasswordResetEmailSender>();
                services.AddSingleton<IPasswordResetEmailSender>(sender);
            });
        });
        using HttpClient client = factory.CreateClient();
        try
        {
            using HttpResponseMessage registered = await client.PostAsJsonAsync("/auth/register",
                new RegisterRequest("Recovery", "User", email, "ValidPass!"));
            Assert.Equal(HttpStatusCode.Created, registered.StatusCode);
            using HttpResponseMessage response = await client.PostAsJsonAsync("/auth/forgot-password", new { email })
                .WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
            await sender.Entered.Task.WaitAsync(TimeSpan.FromSeconds(20));
            Assert.False(sender.Release.Task.IsCompleted);
        }
        finally
        {
            sender.Release.TrySetResult(true);
        }
    }

    private sealed class RecoveryClock(DateTimeOffset now) : TimeProvider
    {
        private long _ticks = now.UtcTicks;

        // Reads the application clock safely while a background worker is also using it.
        public override DateTimeOffset GetUtcNow() => new(Interlocked.Read(ref _ticks), TimeSpan.Zero);

        // Advances deterministic time without delaying the test or changing the system clock.
        public void Advance(TimeSpan amount) => Interlocked.Add(ref _ticks, amount.Ticks);
    }

    private sealed class BlockingResetSender(string recipient) : IPasswordResetEmailSender
    {
        public TaskCompletionSource<bool> Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        // Blocks only the test recipient until the test releases it; other queued jobs are irrelevant.
        public async Task SendAsync(PasswordResetEmail email, CancellationToken cancellationToken = default)
        {
            if (email.RecipientEmail != recipient) return;
            Entered.TrySetResult(true);
            await Release.Task.WaitAsync(cancellationToken);
        }
    }

    // The configured per-IP limit is independent of whether the requested account exists.
    [Fact]
    public async Task Forgot_password_should_enforce_configured_ip_limit()
    {
        using var factory = fixture.Factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["PasswordRecovery:RateLimits:RequestPermitLimit"] = "2" })));
        using HttpClient client = factory.CreateClient();
        for (int attempt = 0; attempt < 2; attempt++)
        {
            using HttpResponseMessage accepted = await client.PostAsJsonAsync("/auth/forgot-password",
                new { email = $"limit-{Guid.NewGuid():N}@example.com" });
            Assert.Equal(HttpStatusCode.Accepted, accepted.StatusCode);
        }
        using HttpResponseMessage limited = await client.PostAsJsonAsync("/auth/forgot-password",
            new { email = $"limit-{Guid.NewGuid():N}@example.com" });
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
    }

    // The HTTP response completes before a background worker sends a purpose-specific reset token.
    [Fact]
    public async Task Recovery_request_should_deliver_a_thirty_minute_token_in_background()
    {
        using var factory = fixture.Factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["PasswordRecovery:WorkerEnabled"] = "true" })));
        using HttpClient client = factory.CreateClient();
        string email = $"delivery-{Guid.NewGuid():N}@example.com";
        using HttpResponseMessage registration = await client.PostAsJsonAsync("/auth/register",
            new RegisterRequest("Recovery", "User", email, "ValidPass!"));
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
        DateTime before = DateTime.UtcNow;
        using HttpResponseMessage response = await client.PostAsJsonAsync("/auth/forgot-password", new { email });
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        PasswordResetEmail message = await fixture.Factory.PasswordResetEmails.WaitForAsync(email);
        Assert.StartsWith("pwd_", message.RawToken);
        Assert.Equal(47, message.RawToken.Length);
        Assert.InRange(message.ExpiresAtUtc, before.AddMinutes(30), DateTime.UtcNow.AddMinutes(30));
        Assert.DoesNotContain(message.RawToken, await response.Content.ReadAsStringAsync());
        using HttpResponseMessage wrongPurpose = await client.PostAsJsonAsync(
            "/auth/email-verification/confirm", new { token = message.RawToken });
        Assert.Equal(HttpStatusCode.BadRequest, wrongPurpose.StatusCode);
    }

    // Both addresses receive the same response; requesting recovery must not change the password.
    [Fact]
    public async Task Forgot_password_should_accept_known_and_unknown_addresses_identically()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        string email = $"recovery-{Guid.NewGuid():N}@example.com";
        using HttpResponseMessage registration = await client.PostAsJsonAsync("/auth/register",
            new RegisterRequest("Recovery", "User", email, "ValidPass!"));
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);

        using HttpResponseMessage known = await client.PostAsJsonAsync("/auth/forgot-password", new { email });
        using HttpResponseMessage unknown = await client.PostAsJsonAsync("/auth/forgot-password",
            new { email = $"unknown-{Guid.NewGuid():N}@example.com" });
        Assert.Equal(HttpStatusCode.Accepted, known.StatusCode);
        Assert.Equal(known.StatusCode, unknown.StatusCode);
        Assert.Equal(await known.Content.ReadAsStringAsync(), await unknown.Content.ReadAsStringAsync());
        using HttpResponseMessage login = await client.PostAsJsonAsync("/auth/login",
            new LoginRequest(email, "ValidPass!"));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }
}
```

### C:/Users/Hamza/Documents/OpsDesk API/tests/OpsDesk.Tests/Email/SmtpTransportLifecycleTests.cs

```csharp
using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Options;
using OpsDesk.Application.Auth.Models;
using OpsDesk.Infrastructure.Email;

namespace OpsDesk.Tests.Email;

public sealed class SmtpTransportLifecycleTests
{
    // A server may accept DATA yet stop responding to QUIT; delivery must still complete successfully.
    [Fact]
    public async Task Accepted_email_should_not_wait_for_a_quit_reply()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        using var shutdown = new CancellationTokenSource();
        Task server = RunServerAsync(listener, shutdown.Token);
        var settings = new EmailSettings
        {
            Host = "127.0.0.1", Port = ((IPEndPoint)listener.LocalEndpoint).Port,
            FromAddress = "noreply@opsdesk.local", FromName = "OpsDesk"
        };
        var sender = new SmtpPasswordResetEmailSender(Options.Create(settings));
        try
        {
            await sender.SendAsync(new PasswordResetEmail("recipient@example.com", "pwd_test", DateTime.UtcNow.AddMinutes(30)))
                .WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            await shutdown.CancelAsync();
            listener.Stop();
            await server;
        }
    }

    // Implements only the SMTP commands needed by this test and deliberately never answers QUIT.
    private static async Task RunServerAsync(TcpListener listener, CancellationToken cancellationToken)
    {
        try
        {
            using TcpClient socket = await listener.AcceptTcpClientAsync(cancellationToken);
            using NetworkStream stream = socket.GetStream();
            using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
            using var writer = new StreamWriter(stream, Encoding.ASCII, leaveOpen: true)
            {
                AutoFlush = true, NewLine = "\r\n"
            };
            await writer.WriteLineAsync("220 localhost test SMTP");
            bool readingData = false;
            while (await reader.ReadLineAsync(cancellationToken) is { } line)
            {
                if (readingData)
                {
                    if (line != ".") continue;
                    readingData = false;
                    await writer.WriteLineAsync("250 Message accepted");
                }
                else if (line.StartsWith("DATA", StringComparison.Ordinal))
                {
                    readingData = true;
                    await writer.WriteLineAsync("354 Send message");
                }
                else if (!line.StartsWith("QUIT", StringComparison.Ordinal))
                {
                    await writer.WriteLineAsync("250 OK");
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
    }
}
```

### C:/Users/Hamza/Documents/OpsDesk API/src/OpsDesk.Infrastructure/Persistence/Repositories/PasswordResetTokenRepository.cs

```csharp
using Microsoft.EntityFrameworkCore;
using OpsDesk.Application.Auth.Interfaces;
using OpsDesk.Domain.Entities;

namespace OpsDesk.Infrastructure.Persistence.Repositories;

public sealed class PasswordResetTokenRepository(OpsDeskDbContext database) : IPasswordResetTokenRepository
{
    // Locks the account, not only the token row, so first-time concurrent requests also serialize.
    public async Task<bool> TryReplaceAsync(PasswordResetToken token,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        List<User> users = await database.Users.FromSqlInterpolated(
            $"SELECT * FROM users WHERE id = {token.UserId} FOR UPDATE")
            .AsNoTracking().ToListAsync(cancellationToken);
        if (users.Count == 0) return false;
        DateTime cooldownBoundary = token.CreatedAtUtc.AddSeconds(-60);
        if (await database.PasswordResetTokens.AnyAsync(existing => existing.UserId == token.UserId
            && existing.CreatedAtUtc > cooldownBoundary, cancellationToken)) return false;

        await database.PasswordResetTokens.Where(existing => existing.UserId == token.UserId)
            .ExecuteDeleteAsync(cancellationToken);
        database.PasswordResetTokens.Add(token);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    // Retains issuance time for throttling while invalidating only the failed delivery's token.
    public Task RevokeAsync(Guid tokenId, DateTime revokedAtUtc, CancellationToken cancellationToken = default)
    {
        return database.PasswordResetTokens.Where(token => token.Id == tokenId && token.RevokedAtUtc == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(token => token.RevokedAtUtc, revokedAtUtc),
                cancellationToken);
    }
}
```

### C:/Users/Hamza/Documents/OpsDesk API/src/OpsDesk.Infrastructure/Persistence/Repositories/PasswordRecoveryQueue.cs

```csharp
using OpsDesk.Application.Auth.Interfaces;
using OpsDesk.Application.Auth.Models;
using Microsoft.EntityFrameworkCore;
using OpsDesk.Infrastructure.Persistence.Entities;

namespace OpsDesk.Infrastructure.Persistence.Repositories;

public sealed class PasswordRecoveryQueue(OpsDeskDbContext database) : IPasswordRecoveryQueue
{
    // Claims a job in a short transaction, releasing database locks before any SMTP operation.
    public async Task<PasswordRecoveryWorkItem?> ClaimAsync(DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        List<PasswordRecoveryJob> jobs = await database.PasswordRecoveryJobs.FromSqlInterpolated(
            $"SELECT * FROM password_recovery_jobs WHERE available_at_utc <= {nowUtc} ORDER BY available_at_utc, id LIMIT 1 FOR UPDATE SKIP LOCKED")
            .ToListAsync(cancellationToken);
        PasswordRecoveryJob? job = jobs.SingleOrDefault();
        if (job is null) return null;
        job.LeaseId = Guid.NewGuid();
        job.AvailableAtUtc = nowUtc.AddMinutes(2);
        job.Attempts++;
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new PasswordRecoveryWorkItem(job.Id, job.Email, job.RequestedAtUtc, job.LeaseId.Value, job.Attempts);
    }

    // A stale worker cannot delete a job that has since been leased by another worker.
    public Task CompleteAsync(PasswordRecoveryWorkItem job, CancellationToken cancellationToken = default)
    {
        return database.PasswordRecoveryJobs.Where(row => row.Id == job.Id && row.LeaseId == job.LeaseId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    // Retries after sixty seconds, with at most three delivery attempts for a queued request.
    public Task RetryAsync(PasswordRecoveryWorkItem job, DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        if (job.Attempts >= 3) return CompleteAsync(job, cancellationToken);
        return database.PasswordRecoveryJobs.Where(row => row.Id == job.Id && row.LeaseId == job.LeaseId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(row => row.LeaseId, (Guid?)null)
                .SetProperty(row => row.AvailableAtUtc, nowUtc.AddSeconds(60)), cancellationToken);
    }

    // Uses the same INSERT path for every syntactically valid email address.
    public async Task EnqueueAsync(string email, DateTime requestedAtUtc,
        CancellationToken cancellationToken = default)
    {
        database.PasswordRecoveryJobs.Add(new PasswordRecoveryJob
        {
            Email = email,
            RequestedAtUtc = requestedAtUtc,
            AvailableAtUtc = requestedAtUtc
        });
        await database.SaveChangesAsync(cancellationToken);
    }
}
```

### C:/Users/Hamza/Documents/OpsDesk API/src/OpsDesk.Infrastructure/Authentication/PasswordResetTokenGenerator.cs

```csharp
using OpsDesk.Application.Auth.Interfaces;
using OpsDesk.Application.Auth.Models;

namespace OpsDesk.Infrastructure.Authentication;

public sealed class PasswordResetTokenGenerator(IEmailVerificationTokenGenerator generator)
    : IPasswordResetTokenGenerator
{
    // Separates password-reset credentials from invitations and verification tokens.
    public GeneratedPasswordResetToken GenerateToken()
    {
        string rawToken = "pwd_" + generator.GenerateToken().RawToken;
        return new GeneratedPasswordResetToken(rawToken, generator.ComputeHash(rawToken));
    }
}
```

### C:/Users/Hamza/Documents/OpsDesk API/src/OpsDesk.Infrastructure/Email/SmtpPasswordResetEmailSender.cs

```csharp
using Microsoft.Extensions.Options;
using MimeKit;
using OpsDesk.Application.Auth.Interfaces;
using OpsDesk.Application.Auth.Models;

namespace OpsDesk.Infrastructure.Email;

public sealed class SmtpPasswordResetEmailSender(IOptions<EmailSettings> settings) : IPasswordResetEmailSender
{
    // Uses the existing SMTP transport; the browser reset form belongs to the later frontend phase.
    public async Task SendAsync(PasswordResetEmail email, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(email);
        using var message = new MimeMessage();
        message.From.Add(new MailboxAddress(settings.Value.FromName, settings.Value.FromAddress));
        message.To.Add(new MailboxAddress(string.Empty, email.RecipientEmail));
        message.Subject = "Reset your OpsDesk password";
        message.Body = new TextPart("plain")
        {
            Text = $"A password reset was requested for your OpsDesk account.\n\n" +
                $"Reset token: {email.RawToken}\nExpires at {email.ExpiresAtUtc:O} (UTC).\n\n" +
                "Use this token with your new password. Do not share it. " +
                "If you did not request this, you can ignore this email."
        };
        await SmtpEmailTransport.SendAsync(settings.Value, message, cancellationToken);
    }
}
```

### C:/Users/Hamza/Documents/OpsDesk API/src/OpsDesk.Infrastructure/Persistence/Migrations/20260906130455_AddPasswordResetTokens.cs

```csharp
using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpsDesk.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPasswordResetTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "password_reset_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    revoked_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_password_reset_tokens", x => x.id);
                    table.CheckConstraint("ck_password_reset_tokens_expiry", "expires_at_utc > created_at_utc");
                    table.ForeignKey(
                        name: "FK_password_reset_tokens_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_password_reset_tokens_token_hash",
                table: "password_reset_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_password_reset_tokens_user_id",
                table: "password_reset_tokens",
                column: "user_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "password_reset_tokens");
        }
    }
}
```

### C:/Users/Hamza/Documents/OpsDesk API/src/OpsDesk.Infrastructure/Persistence/Migrations/20260906125657_AddPasswordRecoveryQueue.cs

```csharp
using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpsDesk.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPasswordRecoveryQueue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "password_recovery_jobs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    requested_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    available_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    lease_id = table.Column<Guid>(type: "uuid", nullable: true),
                    attempts = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_password_recovery_jobs", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_password_recovery_jobs_available_at_utc_id",
                table: "password_recovery_jobs",
                columns: new[] { "available_at_utc", "id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "password_recovery_jobs");
        }
    }
}
```

### C:/Users/Hamza/Documents/OpsDesk API/src/OpsDesk.Application/Auth/Services/PasswordRecoveryService.cs

```csharp
using OpsDesk.Application.Auth.Interfaces;
using OpsDesk.Application.Auth.Models;
using OpsDesk.Domain.Entities;

namespace OpsDesk.Application.Auth.Services;

public sealed class PasswordRecoveryService(
    IEmailValidator emailValidator, IPasswordRecoveryQueue queue, TimeProvider timeProvider,
    IUserRepository users, IPasswordResetTokenGenerator generator,
    IPasswordResetTokenRepository tokens, IPasswordResetEmailSender sender)
    : IPasswordRecoveryService
{
    // Both known and unknown addresses follow the same durable enqueue path.
    public Task RequestAsync(string email, CancellationToken cancellationToken = default)
    {
        emailValidator.Validate(email);
        return queue.EnqueueAsync(UserInputNormalizer.NormalizeEmail(email),
            timeProvider.GetUtcNow().UtcDateTime, cancellationToken);
    }

    // Runs outside HTTP: checks eligibility, issues a token atomically, then sends it without a DB lock.
    public async Task SendAsync(string email, DateTime requestedAtUtc,
        CancellationToken cancellationToken = default)
    {
        DateTime nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        if (requestedAtUtc > nowUtc || requestedAtUtc.AddMinutes(30) <= nowUtc) return;
        User? user = await users.GetByEmailAsync(email, cancellationToken);
        if (user is null || user.CreatedAtUtc > requestedAtUtc || string.IsNullOrEmpty(user.PasswordHash)) return;

        GeneratedPasswordResetToken generated = generator.GenerateToken();
        PasswordResetToken token = PasswordResetToken.Create(user.Id, generated.TokenHash, nowUtc);
        if (!await tokens.TryReplaceAsync(token, cancellationToken)) return;
        try
        {
            await sender.SendAsync(new PasswordResetEmail(email, generated.RawToken, token.ExpiresAtUtc),
                cancellationToken);
        }
        catch
        {
            // Cleanup is bounded and independent of an expired delivery cancellation token.
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await tokens.RevokeAsync(token.Id, timeProvider.GetUtcNow().UtcDateTime, cleanup.Token);
            throw;
        }
    }
}
```

### C:/Users/Hamza/Documents/OpsDesk API/src/OpsDesk.Infrastructure/Persistence/Entities/PasswordRecoveryJob.cs

```csharp
namespace OpsDesk.Infrastructure.Persistence.Entities;

// Queue bookkeeping is a persistence detail, not a business entity.
public sealed class PasswordRecoveryJob
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = string.Empty;
    public DateTime RequestedAtUtc { get; set; }
    public DateTime AvailableAtUtc { get; set; }
    public Guid? LeaseId { get; set; }
    public int Attempts { get; set; }
}
```

### C:/Users/Hamza/Documents/OpsDesk API/src/OpsDesk.Domain/Entities/PasswordResetToken.cs

```csharp
namespace OpsDesk.Domain.Entities;

public sealed class PasswordResetToken
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }

    private PasswordResetToken() { }

    // Creates a purpose-specific reset credential with the approved thirty-minute lifetime.
    public static PasswordResetToken Create(Guid userId, string tokenHash, DateTime createdAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);
        if (userId == Guid.Empty || tokenHash.Length != 64 || createdAtUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("A reset token requires a User, SHA-256 hash, and UTC creation time.");
        return new PasswordResetToken
        {
            Id = Guid.NewGuid(), UserId = userId, TokenHash = tokenHash,
            CreatedAtUtc = createdAtUtc, ExpiresAtUtc = createdAtUtc.AddMinutes(30)
        };
    }
}
```

### C:/Users/Hamza/Documents/OpsDesk API/src/OpsDesk.Infrastructure/Persistence/Configurations/PasswordResetTokenConfiguration.cs

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpsDesk.Domain.Entities;

namespace OpsDesk.Infrastructure.Persistence.Configurations;

public sealed class PasswordResetTokenConfiguration : IEntityTypeConfiguration<PasswordResetToken>
{
    // Keeps one reset credential per account and never persists a raw token.
    public void Configure(EntityTypeBuilder<PasswordResetToken> builder)
    {
        builder.ToTable("password_reset_tokens", table => table.HasCheckConstraint(
            "ck_password_reset_tokens_expiry", "expires_at_utc > created_at_utc"));
        builder.HasKey(token => token.Id);
        builder.Property(token => token.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(token => token.UserId).HasColumnName("user_id");
        builder.Property(token => token.TokenHash).HasColumnName("token_hash").HasMaxLength(64).IsRequired();
        builder.Property(token => token.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(token => token.ExpiresAtUtc).HasColumnName("expires_at_utc");
        builder.Property(token => token.RevokedAtUtc).HasColumnName("revoked_at_utc");
        builder.HasIndex(token => token.UserId).IsUnique();
        builder.HasIndex(token => token.TokenHash).IsUnique();
        builder.HasOne<User>().WithMany().HasForeignKey(token => token.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
```

### C:/Users/Hamza/Documents/OpsDesk API/src/OpsDesk.Infrastructure/Persistence/Configurations/PasswordRecoveryJobConfiguration.cs

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpsDesk.Infrastructure.Persistence.Entities;

namespace OpsDesk.Infrastructure.Persistence.Configurations;

public sealed class PasswordRecoveryJobConfiguration : IEntityTypeConfiguration<PasswordRecoveryJob>
{
    // Maps the durable work queue; no password or raw token is persisted here.
    public void Configure(EntityTypeBuilder<PasswordRecoveryJob> builder)
    {
        builder.ToTable("password_recovery_jobs");
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
```

### C:/Users/Hamza/Documents/OpsDesk API/src/OpsDesk.Application/Auth/Models/PasswordResetEmail.cs

```csharp
namespace OpsDesk.Application.Auth.Models;

public sealed record PasswordResetEmail(string RecipientEmail, string RawToken, DateTime ExpiresAtUtc);
```

### C:/Users/Hamza/Documents/OpsDesk API/src/OpsDesk.Application/Auth/Models/PasswordRecoveryWorkItem.cs

```csharp
namespace OpsDesk.Application.Auth.Models;

public sealed record PasswordRecoveryWorkItem(
    Guid Id, string Email, DateTime RequestedAtUtc, Guid LeaseId, int Attempts);
```

### C:/Users/Hamza/Documents/OpsDesk API/src/OpsDesk.Application/Auth/Models/GeneratedPasswordResetToken.cs

```csharp
namespace OpsDesk.Application.Auth.Models;

public sealed record GeneratedPasswordResetToken(string RawToken, string TokenHash);
```

### C:/Users/Hamza/Documents/OpsDesk API/src/OpsDesk.Application/Auth/Interfaces/IPasswordResetTokenRepository.cs

```csharp
using OpsDesk.Domain.Entities;

namespace OpsDesk.Application.Auth.Interfaces;

public interface IPasswordResetTokenRepository
{
    // Serializes issuance per account and enforces the sixty-second sending cooldown.
    Task<bool> TryReplaceAsync(PasswordResetToken token, CancellationToken cancellationToken = default);

    // Invalidates a failed delivery without revoking a newer token issued for the same account.
    Task RevokeAsync(Guid tokenId, DateTime revokedAtUtc, CancellationToken cancellationToken = default);
}
```

### C:/Users/Hamza/Documents/OpsDesk API/src/OpsDesk.Application/Auth/Interfaces/IPasswordResetTokenGenerator.cs

```csharp
using OpsDesk.Application.Auth.Models;

namespace OpsDesk.Application.Auth.Interfaces;

public interface IPasswordResetTokenGenerator
{
    // Returns the raw email credential and the hash used for persistence.
    GeneratedPasswordResetToken GenerateToken();
}
```

### C:/Users/Hamza/Documents/OpsDesk API/src/OpsDesk.Application/Auth/Interfaces/IPasswordResetEmailSender.cs

```csharp
using OpsDesk.Application.Auth.Models;

namespace OpsDesk.Application.Auth.Interfaces;

public interface IPasswordResetEmailSender
{
    // Delivers a recovery token without including the user's password.
    Task SendAsync(PasswordResetEmail email, CancellationToken cancellationToken = default);
}
```

### C:/Users/Hamza/Documents/OpsDesk API/src/OpsDesk.Application/Auth/Interfaces/IPasswordRecoveryService.cs

```csharp
namespace OpsDesk.Application.Auth.Interfaces;

public interface IPasswordRecoveryService
{
    // Accepts a recovery request without revealing whether an account exists.
    Task RequestAsync(string email, CancellationToken cancellationToken = default);

    // Processes a queued request; unknown or ineligible accounts produce no email.
    Task SendAsync(string email, DateTime requestedAtUtc, CancellationToken cancellationToken = default);
}
```

### C:/Users/Hamza/Documents/OpsDesk API/src/OpsDesk.Application/Auth/Interfaces/IPasswordRecoveryQueue.cs

```csharp
using OpsDesk.Application.Auth.Models;

namespace OpsDesk.Application.Auth.Interfaces;

public interface IPasswordRecoveryQueue
{
    // Persists the request independently of account lookup and SMTP delivery.
    Task EnqueueAsync(string email, DateTime requestedAtUtc, CancellationToken cancellationToken = default);

    // Leases one pending job; another worker may recover it if the lease expires.
    Task<PasswordRecoveryWorkItem?> ClaimAsync(DateTime nowUtc, CancellationToken cancellationToken = default);

    // Deletes a completed job only when the caller still owns its lease.
    Task CompleteAsync(PasswordRecoveryWorkItem job, CancellationToken cancellationToken = default);

    // Releases a failed job for a later attempt, or discards it after the retry limit.
    Task RetryAsync(PasswordRecoveryWorkItem job, DateTime nowUtc, CancellationToken cancellationToken = default);
}
```

### C:/Users/Hamza/Documents/OpsDesk API/src/OpsDesk.Api/Controllers/PasswordRecoveryController.cs

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using OpsDesk.Api.RateLimiting;
using OpsDesk.Application.Auth.DTOs;
using OpsDesk.Application.Auth.Interfaces;

namespace OpsDesk.Api.Controllers;

[ApiController]
[Route("auth")]
public sealed class PasswordRecoveryController(IPasswordRecoveryService recovery) : ControllerBase
{
    // Acknowledges the durable request without exposing account eligibility or delivery state.
    [AllowAnonymous]
    [HttpPost("forgot-password")]
    [EnableRateLimiting(PasswordRecoveryRateLimiting.RequestPolicy)]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> ForgotPassword(
        ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        await recovery.RequestAsync(request.Email, cancellationToken);
        return Accepted(new { message = "If the account is eligible, password recovery instructions will be sent." });
    }
}
```

### C:/Users/Hamza/Documents/OpsDesk API/src/OpsDesk.Api/RateLimiting/PasswordRecoveryRateLimiting.cs

```csharp
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace OpsDesk.Api.RateLimiting;

public static class PasswordRecoveryRateLimiting
{
    public const string RequestPolicy = "password-recovery-request";

    // Registers an endpoint-specific, per-IP limit; account existence never influences the partition.
    public static IServiceCollection AddPasswordRecoveryRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(_ => { });
        services.AddOptions<RateLimiterOptions>().Configure<IConfiguration>((options, configuration) =>
        {
            int permitLimit = configuration.GetValue("PasswordRecovery:RateLimits:RequestPermitLimit", 5);
            int windowMinutes = configuration.GetValue("PasswordRecovery:RateLimits:WindowMinutes", 15);
            if (permitLimit is < 1 or > 1000 || windowMinutes is < 1 or > 1440)
                throw new InvalidOperationException("Password recovery rate limits are outside their supported range.");
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(RequestPolicy, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.MapToIPv6().ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permitLimit,
                    Window = TimeSpan.FromMinutes(windowMinutes),
                    QueueLimit = 0,
                    AutoReplenishment = true
                }));
        });
        return services;
    }
}
```

### C:/Users/Hamza/Documents/OpsDesk API/src/OpsDesk.Api/BackgroundServices/PasswordRecoveryWorker.cs

```csharp
using OpsDesk.Application.Auth.Interfaces;
using OpsDesk.Application.Auth.Models;

namespace OpsDesk.Api.BackgroundServices;

public sealed class PasswordRecoveryWorker(IServiceScopeFactory scopes, IConfiguration configuration,
    TimeProvider timeProvider, ILogger<PasswordRecoveryWorker> logger) : BackgroundService
{
    // Runs independently of HTTP and gives each job a fresh set of scoped services and DbContext.
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue("PasswordRecovery:WorkerEnabled", true)) return;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        try
        {
            do
            {
                try
                {
                    for (int processed = 0; processed < 10; processed++)
                    {
                        if (!await ProcessNextAsync(stoppingToken)) break;
                    }
                }
                catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
                {
                    // Exception messages can contain SMTP details; log only type, never email/token payloads.
                    logger.LogError("Password recovery queue failed ({ErrorType}); retrying on the next poll.",
                        exception.GetType().Name);
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            logger.LogDebug("Password recovery worker stopped; leased work remains recoverable.");
            return;
        }
    }

    // Leases one job, bounds its delivery time, and records success or a retry without exposing it over HTTP.
    private async Task<bool> ProcessNextAsync(CancellationToken stoppingToken)
    {
        await using AsyncServiceScope scope = scopes.CreateAsyncScope();
        var queue = scope.ServiceProvider.GetRequiredService<IPasswordRecoveryQueue>();
        DateTime nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        PasswordRecoveryWorkItem? job = await queue.ClaimAsync(nowUtc, stoppingToken);
        if (job is null) return false;
        if (job.Attempts > 3 || job.RequestedAtUtc.AddMinutes(30) <= nowUtc)
        {
            await queue.CompleteAsync(job, stoppingToken);
            return true;
        }
        try
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            deadline.CancelAfter(TimeSpan.FromSeconds(30));
            var service = scope.ServiceProvider.GetRequiredService<IPasswordRecoveryService>();
            await service.SendAsync(job.Email, job.RequestedAtUtc, deadline.Token);
            await queue.CompleteAsync(job, stoppingToken);
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            logger.LogWarning("Password recovery job {JobId} attempt {Attempt} failed ({ErrorType}).",
                job.Id, job.Attempts, exception.GetType().Name);
            await queue.RetryAsync(job, timeProvider.GetUtcNow().UtcDateTime, stoppingToken);
        }
        return true;
    }
}
```

### C:/Users/Hamza/Documents/OpsDesk API/src/OpsDesk.Application/Auth/DTOs/ForgotPasswordRequest.cs

```csharp
namespace OpsDesk.Application.Auth.DTOs;

public sealed record ForgotPasswordRequest(string Email);
```

### C:/Users/Hamza/Documents/OpsDesk API/src/OpsDesk.Infrastructure/Email/SmtpEmailTransport.cs

```csharp
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace OpsDesk.Infrastructure.Email;

internal static class SmtpEmailTransport
{
    // Disposes the connection without an extra QUIT exchange that could invalidate successful delivery.
    internal static async Task SendAsync(
        EmailSettings settings, MimeMessage message, CancellationToken cancellationToken)
    {
        using var client = new SmtpClient();
        await client.ConnectAsync(settings.Host, settings.Port,
            settings.UseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.None,
            cancellationToken);
        await client.SendAsync(message, cancellationToken);
    }
}
```

### C:/Users/Hamza/Documents/OpsDesk API/src/OpsDesk.Infrastructure/DependencyInjection.cs

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpsDesk.Application.Auth.Interfaces;
using OpsDesk.Application.Invitations.Interfaces;
using OpsDesk.Application.Tickets.Interfaces;
using OpsDesk.Infrastructure.Authentication;
using OpsDesk.Infrastructure.Email;
using OpsDesk.Infrastructure.Persistence;
using OpsDesk.Infrastructure.Persistence.Repositories;
using OpsDesk.Infrastructure.Seed;

namespace OpsDesk.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string connectionString =
            configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "DefaultConnection connection string was not found.");
        services.Configure<JwtSettings>(
            configuration.GetSection(JwtSettings.SectionName));
        services.Configure<AdminSeedSettings>(
            configuration.GetSection(AdminSeedSettings.SectionName));
        services.AddOptions<EmailSettings>()
            .Bind(
                configuration.GetSection(
                    EmailSettings.SectionName))
            .ValidateOnStart();
        services.AddSingleton<
            IValidateOptions<EmailSettings>,
            EmailSettingsValidator>();

        services.AddDbContext<OpsDeskDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IPasswordRecoveryQueue, PasswordRecoveryQueue>();
        services.AddScoped<IPasswordResetTokenRepository, PasswordResetTokenRepository>();
        services.AddSingleton<IPasswordResetTokenGenerator, PasswordResetTokenGenerator>();
        services.AddSingleton<IPasswordResetEmailSender, SmtpPasswordResetEmailSender>();
        services.AddScoped<IInvitationRepository, InvitationRepository>();
        services.AddSingleton<IInvitationTokenGenerator, InvitationTokenGenerator>();
        services.AddSingleton<IInvitationEmailSender, SmtpInvitationEmailSender>();
        services.AddScoped<
            IEmailVerificationTokenRepository,
            EmailVerificationTokenRepository>();
        services.AddScoped<ITicketRepository, TicketRepository>();
        services.AddScoped<AdminUserSeeder>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddSingleton<
            IEmailVerificationTokenGenerator,
            EmailVerificationTokenGenerator>();
        services.AddSingleton<
            IEmailVerificationEmailSender,
            SmtpEmailVerificationEmailSender>();

        return services;
    }
}
```

### C:/Users/Hamza/Documents/OpsDesk API/src/OpsDesk.Infrastructure/Persistence/OpsDeskDbContext.cs

```csharp
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

    public DbSet<PasswordRecoveryJob> PasswordRecoveryJobs => Set<PasswordRecoveryJob>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();

    public DbSet<UserInvitation> UserInvitations => Set<UserInvitation>();

    public DbSet<Ticket> Tickets => Set<Ticket>();

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
```

### C:/Users/Hamza/Documents/OpsDesk API/src/OpsDesk.Api/Program.cs

```csharp
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using OpsDesk.Api.Authorization;
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

### C:/Users/Hamza/Documents/OpsDesk API/tests/OpsDesk.Tests/Integration/SmtpEmailVerificationEmailSenderTests.cs

```csharp
using System.Net.Http.Json;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.Options;
using OpsDesk.Application.Auth.Interfaces;
using OpsDesk.Application.Auth.Models;
using OpsDesk.Infrastructure.Email;

namespace OpsDesk.Tests.Integration;

public sealed class SmtpEmailVerificationEmailSenderTests :
    IAsyncLifetime
{
    private const int SmtpPort = 1025;
    private const int HttpPort = 8025;

    private readonly IContainer _mailpitContainer =
        new ContainerBuilder("axllent/mailpit:v1.30.0")
            .WithPortBinding(SmtpPort, true)
            .WithPortBinding(HttpPort, true)
            .WithWaitStrategy(
                Wait.ForUnixContainer()
                    .UntilHttpRequestIsSucceeded(
                        request => request
                            .ForPort(HttpPort)
                            .ForPath("/readyz")))
            .Build();

    public Task InitializeAsync()
    {
        return _mailpitContainer.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _mailpitContainer.DisposeAsync();
    }

    [Fact]
    public async Task SendAsync_should_deliver_verification_email_to_mailpit()
    {
        const string rawToken = "email-verification-token";
        var settings = new EmailSettings
        {
            Host = _mailpitContainer.Hostname,
            Port = _mailpitContainer.GetMappedPublicPort(SmtpPort),
            UseSsl = false,
            FromAddress = "noreply@opsdesk.local",
            FromName = "OpsDesk",
            VerificationUrl =
                "http://localhost:5044/auth/email-verification/confirm"
        };
        IEmailVerificationEmailSender sender =
            new SmtpEmailVerificationEmailSender(
                Options.Create(settings));
        var email = new EmailVerificationEmail(
            "customer@example.com",
            rawToken,
            new DateTime(
                2026,
                9,
                4,
                20,
                0,
                0,
                DateTimeKind.Utc));

        await sender.SendAsync(email);

        using var client = new HttpClient
        {
            BaseAddress = new Uri(
                $"http://{_mailpitContainer.Hostname}:" +
                _mailpitContainer.GetMappedPublicPort(HttpPort))
        };
        MailpitMessagesResponse? response =
            await client.GetFromJsonAsync<MailpitMessagesResponse>(
                "/api/v1/messages",
                new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
        MailpitMessageSummary message =
            Assert.Single(
                Assert.IsType<MailpitMessagesResponse>(response)
                    .Messages);
        MailpitAddress recipient = Assert.Single(message.To);
        string htmlBody = await client.GetStringAsync(
            "/view/latest.html");

        Assert.Equal(
            "Verify your OpsDesk email",
            message.Subject);
        Assert.Equal(
            "customer@example.com",
            recipient.Address);
        Assert.Contains(
            settings.VerificationUrl + "?token=" + rawToken,
            htmlBody,
            StringComparison.Ordinal);
    }

    private sealed record MailpitMessagesResponse(
        IReadOnlyList<MailpitMessageSummary> Messages);

    // Verifies the invitation adapter really delivers its distinct content over SMTP.
    [Fact]
    public async Task SendAsync_should_deliver_invitation_email_to_mailpit()
    {
        var settings = new EmailSettings
        {
            Host = _mailpitContainer.Hostname,
            Port = _mailpitContainer.GetMappedPublicPort(SmtpPort),
            FromAddress = "noreply@opsdesk.local",
            FromName = "OpsDesk"
        };
        var sender = new SmtpInvitationEmailSender(Options.Create(settings));
        var email = new OpsDesk.Application.Invitations.Models.InvitationEmail(
            "invited@example.com", OpsDesk.Domain.Enums.UserRole.Agent,
            "inv_test-token", DateTime.UtcNow.AddHours(24));

        await sender.SendAsync(email);

        using var client = new HttpClient
        {
            BaseAddress = new Uri($"http://{_mailpitContainer.Hostname}:" +
                _mailpitContainer.GetMappedPublicPort(HttpPort))
        };
        var response = await client.GetFromJsonAsync<MailpitMessagesResponse>("/api/v1/messages");
        var message = Assert.Single(Assert.IsType<MailpitMessagesResponse>(response).Messages);
        Assert.Equal("You are invited to OpsDesk", message.Subject);
        Assert.Equal(email.RecipientEmail, Assert.Single(message.To).Address);
        string text = await client.GetStringAsync("/view/latest.txt");
        Assert.Contains(email.RawToken, text);
        Assert.Contains("Agent", text);
    }

    private sealed record MailpitMessageSummary(
        string Subject,
        IReadOnlyList<MailpitAddress> To);

    // Verifies the password-reset adapter over a real SMTP connection, including its distinct purpose.
    [Fact]
    public async Task SendAsync_should_deliver_password_reset_email_to_mailpit()
    {
        var settings = new EmailSettings
        {
            Host = _mailpitContainer.Hostname,
            Port = _mailpitContainer.GetMappedPublicPort(SmtpPort),
            FromAddress = "noreply@opsdesk.local", FromName = "OpsDesk"
        };
        var sender = new SmtpPasswordResetEmailSender(Options.Create(settings));
        var email = new PasswordResetEmail("recovery@example.com", "pwd_test-token", DateTime.UtcNow.AddMinutes(30));
        await sender.SendAsync(email);
        using var client = new HttpClient
        {
            BaseAddress = new Uri($"http://{_mailpitContainer.Hostname}:" +
                _mailpitContainer.GetMappedPublicPort(HttpPort))
        };
        var response = await client.GetFromJsonAsync<MailpitMessagesResponse>("/api/v1/messages");
        var message = Assert.Single(Assert.IsType<MailpitMessagesResponse>(response).Messages);
        Assert.Equal("Reset your OpsDesk password", message.Subject);
        Assert.Equal(email.RecipientEmail, Assert.Single(message.To).Address);
        string text = await client.GetStringAsync("/view/latest.txt");
        Assert.Contains(email.RawToken, text);
        Assert.Contains("If you did not request this", text);
    }

    private sealed record MailpitAddress(string Address);
}
```
