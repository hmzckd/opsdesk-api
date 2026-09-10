# AUTH-006: Parola Sifirlama ve Eski Oturumlari Gecersiz Kilma

2026-09-07. Bu belge ikinci/final dilimin aciklamasidir. Sondaki tam kaynak kopyalari incelemek icindir; uygulamanin guncel kaynagi src/ ve tests/ altindadir.
Ilk dilim: [Kurtarma talebi ve arka plan e-postasi](auth-006-password-recovery-request.md).

## Task'in Amaci

Parolasini unutan kullanici, e-postasindaki tek kullanimlik kodla yeni parola belirleyebilsin ve onceki girisleri artik kullanilamasin.

Durum: uygulama ve otomatik dogrulama tamamlandi, AUTH-006 Review. Done veya Git yayini yapilmadi.
Son dogrulama: 290 test basarili, 0 basarisiz, 0 atlanan; build 0 uyari/hata; Slopwatch 0 bulgu.
Bir onceki tam kosu Docker motoruna baglanamadigi icin altyapi hatasi vermisti; Docker hazir olduktan sonraki tam kosu yukaridaki 290/290 sonucudur.

## Kullanicinin Gordugu Akis

1. POST /auth/forgot-password ile email gonderir. 202, istegin kuyruga kaydedildigini belirtir; teslim garantisi degildir.
2. Gelistirmede Mailpit'teki e-postadan pwd_ ile baslayan kodu alir.
3. POST /auth/reset-password icin Token ve NewPassword alanlarini gonderir.
4. 204, sifirlama basarili ve cevap govdesi yok demektir. Bu endpoint JWT uretmez.
5. POST /auth/login ile YENI parolasini kullanir, yeni JWT alir ve Swagger Authorize alanina bunu koyar.

Reset token JWT degildir. Authorize alanina yazilmaz. JSON govdesindeki token alanina gider.
JSON alan adlari istemcide token ve newPassword olarak kullanilir; C# DTO'sunda Token ve NewPassword bulunur.
E-postada tarayici formu henuz yoktur. Bu adim frontend asamasina ertelendi.
Gelistirme gondericisi noreply@opsdesk.local, hedef yerel Mailpit'tir; gercek Gmail teslimi kurulmus sayilmaz.

## Neden Iki Farkli Token Var?

Access token (JWT), zaten giris yapan kisinin kimligini ve yetkisini korunan isteklere tasir.
Reset token, yalnizca parola sifirlamaya izin veren gecici bir kanittir. Kullanici mevcut parolasini bilmedigi icin JWT istemek bu akisi bozardi.
AllowAnonymous, sifirlama isleminin korumasiz oldugu anlamina gelmez: burada kanit JWT yerine tek kullanimlik reset kodudur.
DB'de ham reset kodu degil SHA-256 ozeti saklanir. Kodun kendisi e-postaya gonderilir.
Yeni parola da duz metin saklanmaz; mevcut parola hasher'i ile uretilen PasswordHash saklanir.

## AuthVersion Nedir?

Kullanicinin veritabanindaki oturum surum numarasidir. Ilk deger 0'dir.
JWT olusurken ayni sayi auth_version claim'ine eklenir. Claim, token icindeki ad/deger bilgisidir.
Ornek: DB=0 ve JWT=0 ise oturum surumu uyusur. Parola sifirlaninca DB=1 olur, eski JWT=0 kalir ve reddedilir.
Yeni parolayla giris JWT=1 uretir. JWT imzali oldugu icin istemci 0'i 1'e cevirerek gecerli bir token elde edemez.

Bu kontrol yalnizca /me icinde degil ortak JWT dogrulama noktasindadir; admin ve ticket endpoint'lerini de kapsar.
Surum claim'i olmayan eski JWT'ler de 401 alir. Bu kod devreye girdiginde reset yapmamis kullanicilarin bile eski bicimli token'lari icin yeniden login gerekir.
Reset, rol veya email dogrulama durumunu degistirmez. Dogrulanmamis kisi yeni parolayla giris yapabilir fakat dogrulanmis hesap isteyen ticket endpoint'lerine erisemez.
Her gecerli JWT icin DB'den yalnizca AuthVersion okunur. Ek bir DB sorgusu karsiliginda reset sonrasi ilk yeni istekte eski oturum reddedilir.
Daha once kimlik dogrulamasini gecmis, halen calisan bir HTTP istegi geriye donuk durdurulmaz. Bu ozellik her yeni dogrulama icin gecerlidir.
Cache eklenmedi; eski surumun cache'te kalip iptali geciktirmesi onlendi. DB ulasilamazsa kimlik kontrolu atlanmaz.

## Katmanlar ve Dosyalar

Tum yollar C:/Users/Hamza/Documents/OpsDesk API/ kokune gore verilir; sondaki kaynak basliklarinda her tam yol vardir.
Siniflar klasor olarak birbirinin icine tasinmadi; asagidaki oklar cagri ve bagimlilik anlatir.

HTTP -> Api/Controllers/PasswordRecoveryController.cs
-> Application/Auth/Services/PasswordRecoveryService.cs
-> Infrastructure/Persistence/Repositories/PasswordResetTokenRepository.cs
-> Domain/Entities/User.cs ve PasswordResetToken.cs
-> PostgreSQL transaction commit
-> Controller 204

Korunan yeni istek -> JWT imza/sure/issuer/audience kontrolu
-> Api/Authorization/JwtSessionValidationEvents.cs
-> IUserRepository.GetAuthVersionAsync
-> DB surumu uygunsa endpoint, degilse 401.

- Application/Auth/DTOs/ResetPasswordRequest.cs: yeni dosya; istemcinin sadece Token ve NewPassword gondermesini tanimlar. UserId, Role veya AuthVersion istemciden alinmaz.
- Application/Auth/Interfaces/IPasswordRecoveryService.cs: ResetAsync sozlesmesi eklendi; Controller teknik DB ayrintilarini bilmez.
- Application/Auth/Interfaces/IPasswordResetTokenRepository.cs: GetByHashAsync ve TryResetAsync eklendi; atomik kayit isinin sozlesmesi burada.
- Application/Auth/Interfaces/IPasswordResetTokenGenerator.cs: ComputeHash eklendi; sifreleme teknolojisinin uygulamasi Infrastructure'da kalir.
- Application/Auth/Interfaces/IUserRepository.cs: yalnizca oturum surumunu getiren GetAuthVersionAsync eklendi.
- Application/Authorization/AuthClaimTypes.cs: yeni dosya; auth_version adini JWT ureten ve okuyan iki tarafta ortak tutar.
- Domain/Entities/User.cs: AuthVersion ve ResetPassword eklendi; kullanici durumunu degistirme kurali burada, SQL/JWT burada degil.
- Domain/Entities/PasswordResetToken.cs: ConsumedAtUtc, CanUseAt ve Consume eklendi; kodun gecerli ve kullanilmis olma durumlari burada.
- Infrastructure/Authentication/PasswordResetTokenGenerator.cs: gelen kodun amac/bicim kontrolu ve hash hesaplamasi.
- Infrastructure/Authentication/JwtTokenGenerator.cs: JWT'ye AuthVersion claim'i eklendi.
- Infrastructure/Persistence/Repositories/PasswordResetTokenRepository.cs: kilit, transaction ve EF kayit islemleri.
- Infrastructure/Persistence/Repositories/UserRepository.cs: tum kullaniciyi/parola hash'ini getirmeden sayisal surum sorgusu.
- Infrastructure/Persistence/Configurations/UserConfiguration.cs: auth_version sutun eslemesi ve varsayilan 0.
- Infrastructure/Persistence/Configurations/PasswordResetTokenConfiguration.cs: consumed_at_utc sutun eslemesi.
- Api/Authorization/JwtSessionValidationEvents.cs: yeni dosya; ASP.NET Core kimlik dogrulama yasam dongusune baglanir.
- Api/Controllers/PasswordRecoveryController.cs: yeni reset endpoint'i, HTTP durum kodlari ve rate-limit baglantisi.
- Api/RateLimiting/PasswordRecoveryRateLimiting.cs: reset icin ayri varsayilan 10/15 dakika IP siniri.
- Api/Program.cs: JwtSessionValidationEvents scoped kaydi ve JwtBearerOptions.EventsType baglantisi.
- Api/appsettings.json: ResetPermitLimit=10.
- Tests/Integration/PasswordResetIntegrationTests.cs ve JwtSessionValidationIntegrationTests.cs: yeni HTTP testleri.
- Tests/Integration/PasswordRecoveryIntegrationTests.cs: basarisiz gonderimin iptal edilen kodu ile reset yapilamadigi kontrolu.
- Tests/Auth/EmailVerificationServiceTests.cs: mevcut test repository'si yeni IUserRepository metodunu da uyguluyor; eski testler devre disi birakilmadi.

## Imzalari Okuma Anahtari

public: diger siniflarin cagirabilecegi metot. private: sadece kendi sinifi icinde kullanilan yardimci.
sealed class: bu siniftan miras alinmasini kapatir. record: burada veri tasiyan DTO icin kisa bir C# tanimi.
static: nesne olusturmadan sinif uzerinden kullanilir; AuthClaimTypes sabitleri bu mantiktadir.
async: govdede await kullanilarak beklemeli islemler siralanir; kendiliginden yeni thread acmaz.
Task: ileride tamamlanan islem, donen veri yok. Task<T>: tamamlandiginda T tipinde bir sonuc var.
<T> generic tur yeridir: Task<bool> sonunda true/false verir; bool bir parametre degildir.
?: referans veya deger bos olabilir. Task<int?> sonunda bir sayi ya da kullanici yoksa null verir.
Task<PasswordResetToken?>, metot token ALIYOR demek degil, asenkron olarak token veya null DONDURUYOR demektir.
IActionResult: 204, 400 gibi HTTP cevaplarinin ortak turu. Task<IActionResult>: HTTP sonucu asenkron hazirlanir.
CancellationToken: istemci baglantiyi kapatinca veya islem iptal edilince DB ve beklemelere durma istegi tasir. Parola/reset token'i degildir.
= default: cagiran taraf iptal parametresi vermezse varsayilan iptal sinyali kullanilir.
override: framework'un sanal metodunu kendi davranisimizla uygulariz. JwtBearerEvents.TokenValidated buna ornektir.
await using: islem bitince asenkron kaynagi temizler. Commit edilmeyen transaction dispose edilirken geri alinir.

## Metotlar Tek Tek

### PasswordRecoveryController.ResetPassword
Imza: public async Task<IActionResult> ResetPassword(ResetPasswordRequest request, CancellationToken cancellationToken).
request HTTP JSON'undan gelir; Token ve NewPassword tasir. cancellationToken ASP.NET Core tarafindan istege baglanir.
Girdi kontrolleri/DB burada yazilmaz: recovery.ResetAsync beklenir, sonra NoContent() yani 204 doner.
Api'dedir cunku HTTP'yi bilir. Application'a HTTP cevabi veya Controller tasimiyoruz.
HttpPost adresi belirler; AllowAnonymous JWT zorunlulugunu kaldirir; EnableRateLimiting reset politikasini secer.
ProducesResponseType Swagger'a cevaplari anlatir; tek basina hata yakalama kodu degildir.

### PasswordRecoveryService Constructor
Sinif adindan sonraki parantez primary constructor'dir: DI gerekli nesneleri verir, her istekte new yazmayiz.
emailValidator/queue talep yolunda, timeProvider sure kontrolunde kullanilir.
users hesap sorgusu, generator rastgele kod ve hash, tokens kalici kod islemleri, sender e-posta gonderimi icindir.
Eklenen passwordValidator yeni parolanin mevcut kurallara uymasini; passwordHasher duz paroladan guvenli hash uretilmesini saglar.
Bu bagimliliklar Application interface'leridir; PostgreSQL veya SMTP siniflari Application koduna gomulmez.

### PasswordRecoveryService.ResetAsync
Imza: public async Task ResetAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default).
Task burada AuthResponse veya JWT dondurmez; basariyla tamamlanir ya da hata firlatir.
Once request null kontrolu, kod bicimi/hash'i, on token sorgusu ve zaman kontrolu yapilir.
Sonra parola validator'u ve hasher'i calisir; pahali hash hesabini DB kilidi altinda tutmayiz.
TryResetAsync false donerse kod bu arada tuketilmis/iptal edilmis/degismis olabilir: genel gecersiz kod hatasi verilir.
Application'dadir cunku kurallarin hangi sirayla uygulanacagini duzenler. SQL transaction ayrintisini repository'ye birakir.
ArgumentException mevcut GlobalExceptionHandler tarafindan 400'e cevrilir; ham token veya parola hata mesajina eklenmez.

### PasswordRecoveryService.RequestAsync ve SendAsync
Ilk dilimdeki metotlardir; bu dilimde temel akis degismedi.
RequestAsync(string email, CancellationToken) Task doner; emaili dogrulayip normalize ederek kalici kuyruga yollar.
SendAsync(string email, DateTime requestedAtUtc, CancellationToken) Task doner; uygun hesabi bulur, kod uretir, kaydeder ve e-posta gonderir.
Gonderim hata verirse RevokeAsync ile yalnizca o kodu iptal eder. Temizligin iptal sinyali ayri ve bes saniyeyle sinirlidir.
RequestedAtUtc talebin zamanidir; otuz dakikadan eski isleri teslim etmemek icin kullanilir.
Bunlar Application'da is akisi, gercek SMTP/SQL ise Infrastructure'dadir.

### PasswordResetTokenGenerator.ComputeHash
Imza: public string ComputeHash(string rawToken).
rawToken requestten gelen gercek koddur; donen string veritabaninda aranacak hash'tir.
Bos, 47 karakter olmayan, pwd_ ile baslamayan veya URL-guvenli karakterler disinda karakter iceren girdi reddedilir.
Bicim dogruysa mevcut kriptografik generator SHA-256 hesaplar. Bicimin dogru olmasi DB'de gecerli kod oldugu anlamina gelmez.
Infrastructure'dadir; kriptografi uygulama ayrintisidir. GenerateToken() da ayni ComputeHash metodunu kullanarak uretim/okuma uyumunu korur.

### PasswordResetTokenRepository.GetByHashAsync
Imza: public Task<PasswordResetToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default).
tokenHash generator'den gelir. AsNoTracking ile nesne yalnizca okunur; bu on kontrolde EF degisiklik takibi gerekmez.
SingleOrDefaultAsync tek kayit ya da null dondurur. Hash icin benzersiz indeks vardir.
SQL sorgusu oldugu icin Infrastructure'dadir. Bu sonuc tek basina reset icin kesin izin DEGILDIR.

### PasswordResetTokenRepository.TryResetAsync
Imza: public async Task<bool> TryResetAsync(string tokenHash, string newPasswordHash, CancellationToken cancellationToken = default).
Iki string parametre de Application servisinden gelir; yeni parolanin duz metni repository'ye verilmez.
true ancak transaction commit edildiginde doner; kullanici/kod yoksa veya kod gecersizse false, DB arizasinda exception olur.
Govde: transaction ac -> hash'ten UserId bul -> kullanici satirini FOR UPDATE kilitle -> kodu tekrar oku -> SIMDIKI zamanla tekrar dogrula.
Ardindan user.ResetPassword(newPasswordHash), token.Consume(nowUtc), SaveChangesAsync ve CommitAsync calisir.
FOR UPDATE, ayni hesabi degistirmek isteyen diger transaction'in beklemesini saglar; C# lock degildir ve farkli API instance'larinda da DB tarafinda calisir.
Zamanin kilit alindiktan sonra yeniden okunmasi, kilitte beklerken suresi biten kodu kabul etmemek icindir.
Parola, ConsumedAtUtc ve AuthVersion tek transaction icindedir: ya hepsi kalici olur ya hicbiri.
Infrastructure'dadir cunku bu atomikligi gercek PostgreSQL islemleriyle saglar. Domain metotlari SQL bilmez.

### PasswordResetTokenRepository.TryReplaceAsync ve RevokeAsync
TryReplaceAsync(PasswordResetToken token, CancellationToken) public async Task<bool> doner; token SendAsync'te uretilmis Domain nesnesidir.
Kullanici kilidi altinda 60 saniyelik cooldown kontrol edilir; eski kod silinir, yeni kod eklenir. Uygun degilse false doner.
RevokeAsync(Guid tokenId, DateTime revokedAtUtc, CancellationToken) public async Task doner; veri dondurmez.
tokenId basarisiz teslimin kodudur, revokedAtUtc iptal zamani, cancellationToken bounded cleanup sinyalidir.
Bu dilimde RevokeAsync de once kullanici satirini kilitleyecek transaction'a alindi; sonra ayni tokenId iptal edilir.
Neden: iptal ve reset farkli kilit duzenleri kullanirsa reset eski gecerli durumu okuyup iptalden sonra kayit yapabilirdi.
Uretme, reset ve iptal artik hesap-first duzenini paylasir. Daha yeni kod, eski teslimin hata temizligi yuzunden iptal edilmez.
Inceleme bulgusu statik olarak saptandi ve duzeltildi; SMTP iptali ve iki reset testi var, iptal/reset'in her olasi zamanlamasi tek tek zorlanmadi.

### User.ResetPassword
Imza: public void ResetPassword(string passwordHash).
void veri dondurmez. Parametre onceden hashlenmis paroladir; metot ham parolayi hashlemez.
Bos/500 karakterden uzun hash reddedilir; checked(AuthVersion + 1) tasma varsa hata verir; sonra hash ve surum birlikte nesnede degisir.
private set olan AuthVersion disaridan rastgele atanamaz. Rol ve EmailVerifiedAtUtc'ye dokunulmaz.
Domain'dedir cunku User'in durum degisimini tanimlar. SaveChanges/commit yapmaz; bellekteki degisimi repository kalici hale getirir.

### PasswordResetToken.CanUseAt ve Consume
CanUseAt(DateTime nowUtc) public bool doner. UTC bekler, iptal/tuketilme olmamali ve created <= now < expires olmali.
Consume(DateTime nowUtc) public void doner; CanUseAt false ise hata, true ise ConsumedAtUtc=nowUtc yapar.
nowUtc, repository'nin kilit sonrasi TimeProvider'dan aldigi andir. nullable ConsumedAtUtc null ise henuz kullanilmamistir.
Domain'dedir cunku tek kullanimlilik/sure kurali HTTP veya DB'ye bagli degildir.
Create(Guid userId, string tokenHash, DateTime createdAtUtc) mevcut public static factory'dir; 30 dakikalik yeni nesne olusturur, DB'ye kaydetmez.

### JwtTokenGenerator.GenerateToken
Mevcut public JwtTokenResult GenerateToken(User user) metodudur; user login/register akisindan gelir.
Yeni eklenen claim, user.AuthVersion sayisini InvariantCulture ile kararli metne cevirir; Integer32 bunun sayisal deger oldugunu belirtir.
JWT imzalama ve access token/ExpiresAtUtc sonucu aynidir. Yeni rol veya ekstra refresh token eklenmedi.
Infrastructure'dadir cunku JWT formati, imza anahtari ve kriptografik kutuphane teknoloji ayrintilaridir.
AuthClaimTypes.AuthVersion ortak sabittir; burada ve validator'da ayni claim adini kullaniriz.

### UserRepository.GetAuthVersionAsync
Imza: public Task<int?> GetAuthVersionAsync(Guid userId, CancellationToken cancellationToken = default).
userId dogrulanmis token claim'inden gelir. Select yalnizca sayiyi ister; PasswordHash veya tum User yuklenmez.
int? kullanici yoksa null'u, mevcut ilk surum ise 0'i ayirt eder. Task asenkron DB sonucudur; metotta await olmadigi icin async yazmak zorunda degiliz.
Infrastructure sorguyu uygular; IUserRepository Application'da HTTP/EF bilmeyen sozlesmedir.

### JwtSessionValidationEvents.TokenValidated
Imza: public override async Task TokenValidated(TokenValidatedContext context).
public framework'un cagirdigi metottur; override JwtBearerEvents davranisini uygular; Task bitince dogrulama tamamlanir.
context ASP.NET Core'dan gelir: dogrulanmis Principal, HTTP request ve Fail metodu tasir. Istemciden ayri DTO olarak gelmez.
NameIdentifier ve auth_version icin tam birer claim aranir; duplicate/eksik degerler reddedilir.
Guid.TryParse ve int.TryParse hata firlatmadan degerleri cozer. out Guid userId ve out int version basariliysa cikti degiskenleridir.
DB surumu null veya farkliysa context.Fail cagrilir. Bu bir HTTP cevabi dondurmek degil authentication sonucunu basarisiz isaretlemektir; korunan endpoint 401 olur.
Api'dedir cunku JwtBearerEvents/HTTP context ASP.NET Core ayrintilaridir. DB sorgusu repository'dedir.
Program.cs'deki AddScoped bu sinifi istek omruyle olusturur; EventsType ise JWT mekanizmasina hangi sinifi cagirmasi gerektigini soyleyerek gercek baglantiyi kurar.

### PasswordRecoveryRateLimiting.AddPasswordRecoveryRateLimiting
Mevcut public static IServiceCollection extension metodu; this IServiceCollection services DI kayit listesidir, sonunda ayni liste doner.
Request ve reset icin iki ayri politika vardir. ResetPermitLimit varsayilan 10, ortak pencere 15 dakika, kuyruk kapasitesi 0.
Configure<IConfiguration> nihai konfigurasyonu DI'dan okur; testlerde veya environment'ta verilen limitler kullanilir.
IP bilgisi HTTP baglantisindan gelir. Guvenilir proxy ayari yapilmadan istemcinin forwarding header'i esas alinmaz.
Sinir tek uygulama instance'i icindir; dagitik Redis limiti veya reverse proxy tasarimi bu taskta eklenmedi.
Api'dedir cunku HTTP istek trafigini yonetir, Domain is kurali degildir.

### EF Configure ve Migration Metotlari
UserConfiguration.Configure(EntityTypeBuilder<User> builder) public void: User property'lerini DB sutunlarina esler; AuthVersion icin varsayilan 0 eklenmistir.
PasswordResetTokenConfiguration.Configure(EntityTypeBuilder<PasswordResetToken> builder) public void: nullable ConsumedAtUtc'yi consumed_at_utc'ye esler.
builder EF'nin verdigi esleme nesnesidir; kullanici request'i degildir. Bu metotlar tek basina DB'yi degistirmez.
AddPasswordResetConsumptionAndAuthVersion.Up(MigrationBuilder migrationBuilder) protected override void: users.auth_version ve password_reset_tokens.consumed_at_utc sutunlarini ekler.
Down ayni iki sutunu geri alir; sifirlama gecmisi/surum bilgisi kaybolacagindan gelisiguzel rollback yapilmamali.
protected: yalnizca sinif/miras hiyerarsisinde erisim; override: EF Migration'in metodunu uygular.
Designer.cs migration modelini, OpsDeskDbContextModelSnapshot.cs sonraki migration karsilastirmasi icin son modeli tasir; ikisi CLI tarafindan guncellendi.
Migration test PostgreSQL'ine uygulanarak dogrulandi. Kalici gelistirme veritabanina bu adimda database update uygulanmadi.

## Testler Nasil Calisiyor?

Tests ayri bir projedir; dotnet test xUnit'i calistirir. WebApplicationFactory API'yi test icinde baslatir; HttpClient gercek Controller/DI/service/repository akisina gider.
PostgreSQL Testcontainers ile Docker'da gecici olarak acilir ve migration'lar uygulanir. Bu testler kalici gelistirme DB'sini kullanmaz.
E-posta sinirinda mailbox kullanilir: mailin icindeki kodu test alir; production service/repository taklit edilmez.
Fact tek senaryo, Theory ayni davranisin farkli girdilerle testi, InlineData bu girdileridir. Assert beklenen sonucu kontrol eder.

PasswordResetIntegrationTests icindeki public async Task metotlari test calistiricisinin cagirdigi, veri dondurmeyen asenkron senaryolardir:
- Reset_should_replace_password_consume_token_and_invalidate_existing_sessions: register ve login JWT'lerini alir; reset/replay/eski parola/eski JWT/yeni parola/yeni JWT/verified durumunu HTTP ile kontrol eder.
- Invalid_new_password_should_not_consume_the_token_or_revoke_the_session: gecersiz parola 400, eski oturum 200, ayni kodla duzeltilmis parola 204.
- Reset_should_enforce_the_exact_expiration_boundary(int elapsedSeconds, HttpStatusCode expected): InlineData 1799 saniyede 204, 1800'de 400 bekler; clock uygulama zamanini ilerletir.
- Replacement_token_should_reject_the_previous_token_and_allow_the_new_one: 60 saniye sonra yeniden e-posta alir; eski kod 400, yeni kod 204.
- Concurrent_resets_should_have_exactly_one_winner: iki HTTP istegini Task.WhenAll ile birlikte bekler; biri 204, digeri 400; sadece kazanan parola giris yaptirir.
- Wrong_purpose_and_unknown_tokens_should_preserve_account_access: verification/invitation bicimli/bilinmeyen/bozuk kodlari reddeder; gercek verification kodu sonra calisir, reset dogrulanmis hesap durumunu korur.
- Reset_should_enforce_its_own_configured_ip_limit: siniri 2 yapan test host'unda ucuncu reset 429; forgot endpoint'i ayri butce kullanir.
- Failed_transaction_should_preserve_password_session_and_token_for_retry: SQL yazimi sonrasi commit oncesi altyapi hatasi verir; 500'den sonra eski parola ve JWT calisir, kodla yeniden deneme 204 olur.

CreateRecoveryFactory(TimeProvider? clock = null) private WebApplicationFactory<Program> doner; worker'i acan izole host ve gerekirse test saati kurar.
RegisterAndRequestAsync(HttpClient client) private async Task<(string Email, AuthResponse Session, PasswordResetEmail Message)> doner.
Parantez icindeki tuple uc parcali SONUCTUR, uc parametre degildir. Tek parametre HTTP client'tir. Yardimci register/forgot yapip yakalanan e-postayi geri verir.
ResetClock.GetUtcNow() public override DateTimeOffset doner; Advance(TimeSpan amount) public void ile sadece test saatini ilerletir, Thread.Sleep kullanmaz.
Interlocked, worker ve test ayni saate farkli thread'lerden erisirken sayisal islemi guvenli yapar.

FailResetSaveOnce.SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
public override ValueTask<int> doner; EF'nin altyapi araya girme noktasidir. ValueTask<int> burada tamamlanmis sayisal sonucu ek Task nesnesi uretmeden donebilir.
eventData EF context'ini, result etkilenen kayit sayisini tasir. Test kullanicisinin reset kaydinda bir kez IOException firlatir.
Bu bir repository mock'u degil, gercek SQL yazildiktan sonra transaction commit'inden once hata enjeksiyonudur. Dogrulamalar HTTP uzerindendir.
Test kurulumu bu hata noktasini EF'ye bagladigi icin persistence test altyapisi EF'ye baglidir; public davranis beklentileri private metot cagrilarina bagli degildir.

JwtSessionValidationIntegrationTests.Invalid_session_versions_should_be_rejected_by_all_protected_routes(string? version, bool duplicate):
version null/bozuk/negatif/uyusmayan veya duplicate durumda imzasi GECERLI test JWT'si uretir; /me, /admin/access ve /tickets 401 bekler.
Sonra normal admin JWT'si ile 200 bekleyerek reddin hatali imza veya kapali API yuzunden olmadigini kontrol eder.
Testte yalnizca test konfigurasyonundaki anahtar kullanilir; gercek User Secrets okunmaz.

PasswordRecoveryIntegrationTests mevcut failed-delivery testine eklenen kontrol, teslimi basarisiz kodla reset isteginin 400 oldugunu dogrular.
EmailVerificationServiceTests.InMemoryUserRepository.GetAuthVersionAsync(Guid userId, CancellationToken) Task<int?> doner; listeden surumu/null'u verir. Mevcut test adapter'i yeni interface'i tamamlar.

## Kanit ve Sinirlar

Ilk reset HTTP testi endpoint yokken 404 ile RED oldu; implementation sonrasi GREEN oldu.
Sonraki sinir senaryolari mevcut davranisa regresyon kapsami ekledi; hepsinin ayri production RED asamasi varmis gibi raporlanmiyor.
Alt ajan statik incelemesi iptal/reset kilit uyumsuzlugunu buldu; ana ajan dogrulayip duzeltti.
290 testin tamami basarili. Her olasi cok-worker/proxy/sunucu-zamanlamasi icin matematiksel dogruluk kaniti iddia edilmiyor.
Manuel Swagger/frontend incelemesi kullanici tarafindan ertelendi. Yeni browser reset formu, refresh-token sistemi, SSO ve production SMTP kurulumu eklenmedi.

## Dogrulama Komutlari

Calisma klasoru: C:/Users/Hamza/Documents/OpsDesk API.
Build kaynak kodunu derler; testler davranislari calistirir; Slopwatch test/uyari gizleme gibi kisayollari tarar.

```powershell
dotnet build OpsDesk.sln --no-restore
```
```powershell
dotnet test OpsDesk.sln --no-build --no-restore
```

```powershell
dotnet slopwatch analyze --fail-on warning
```

Migration'i olusturmak icin bu adimda kullanilan komut asagidadir; migration artik var, tekrar olusturmayin.

```powershell
dotnet ef migrations add AddPasswordResetConsumptionAndAuthVersion --project src/OpsDesk.Infrastructure --startup-project src/OpsDesk.Api --output-dir Persistence/Migrations
```

Kalici gelistirme DB'sine uygulama AYRI, henuz yapilmayan islemdir; mevcut iki tabloya yeni sutun ekler.

```powershell
dotnet ef database update --project src/OpsDesk.Infrastructure --startup-project src/OpsDesk.Api
```

## Gercekte Uygulanan Skill'ler

- tdd: ilk HTTP RED/GREEN dongusu ve public davranis uzerinden regresyon testleri.
- codebase-design: atomik reset sorumlulugunu tek repository islemi arkasinda tutma ve Controller'i SQL'den ayirma.
- efcore-patterns: salt okunur sorgular, transaction/row lock ve CLI ile migration.
- dotnet-slopwatch: testleri/uyarilari gizleyen kestirmeler icin sifir-bulgu dogrulamasi.
- Genis urun karari olmadigi icin grilling; commit kapsami olmadigi icin tam code-review skill akisi calistirilmadi. Dar salt-okunur alt ajan incelemesi ayrica yapildi.

## Tam Kaynak Kopyalari

Asagidakiler bu dilimde eklenen veya degisen elle yazilmis dosyalarin tam icerigidir; inceleme kopyasidir.
Otomatik migration Designer ve model snapshot dosyalari tekrar kopyalanmadi; dogrudan Infrastructure/Persistence/Migrations altindan incelenebilir.

### C:/Users/Hamza/Documents/OpsDesk API/src/OpsDesk.Application/Auth/DTOs/ResetPasswordRequest.cs

```csharp
namespace OpsDesk.Application.Auth.DTOs;

public sealed record ResetPasswordRequest(string Token, string NewPassword);
```

### C:/Users/Hamza/Documents/OpsDesk API/src/OpsDesk.Application/Auth/Interfaces/IPasswordRecoveryService.cs

```csharp
using OpsDesk.Application.Auth.DTOs;

namespace OpsDesk.Application.Auth.Interfaces;

public interface IPasswordRecoveryService
{
    // Accepts a recovery request without revealing whether an account exists.
    Task RequestAsync(string email, CancellationToken cancellationToken = default);

    // Resets the password and invalidates previous sessions using a single-use reset credential.
    Task ResetAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default);

    // Processes a queued request; unknown or ineligible accounts produce no email.
    Task SendAsync(string email, DateTime requestedAtUtc, CancellationToken cancellationToken = default);
}
```

### C:/Users/Hamza/Documents/OpsDesk API/src/OpsDesk.Application/Auth/Interfaces/IPasswordResetTokenRepository.cs

```csharp
using OpsDesk.Domain.Entities;

namespace OpsDesk.Application.Auth.Interfaces;

public interface IPasswordResetTokenRepository
{
    // Reads token metadata without tracking; final validity is rechecked under the account lock.
    Task<PasswordResetToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    // Atomically consumes a valid token, changes the password and advances the session version.
    Task<bool> TryResetAsync(string tokenHash, string newPasswordHash, CancellationToken cancellationToken = default);

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

    // Validates the reset-specific format before computing its lookup hash.
    string ComputeHash(string rawToken);
}
```

### C:/Users/Hamza/Documents/OpsDesk API/src/OpsDesk.Application/Auth/Interfaces/IUserRepository.cs

```csharp
using System.Threading;
using System.Threading.Tasks;
using OpsDesk.Domain.Entities;

namespace OpsDesk.Application.Auth.Interfaces;

public interface IUserRepository
{
    // Reads only the server-side session version; null means the account no longer exists.
    Task<int?> GetAuthVersionAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<bool> ExistsByEmailAsync(
        string email,
        CancellationToken cancellationToken = default);

    Task<User?> GetByEmailAsync(
        string email,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves one User by identity without tracking it for changes.
    /// </summary>
    Task<User?> GetByIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves one User with tracking enabled for an update.
    /// </summary>
    Task<User?> GetByIdForUpdateAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        User user,
        CancellationToken cancellationToken = default);
}
```

### C:/Users/Hamza/Documents/OpsDesk API/src/OpsDesk.Application/Auth/Services/PasswordRecoveryService.cs

```csharp
using OpsDesk.Application.Auth.Interfaces;
using OpsDesk.Application.Auth.Models;
using OpsDesk.Domain.Entities;
using OpsDesk.Application.Auth.DTOs;

namespace OpsDesk.Application.Auth.Services;

public sealed class PasswordRecoveryService(
    IEmailValidator emailValidator, IPasswordRecoveryQueue queue, TimeProvider timeProvider,
    IUserRepository users, IPasswordResetTokenGenerator generator,
    IPasswordResetTokenRepository tokens, IPasswordResetEmailSender sender,
    IPasswordValidator passwordValidator, IPasswordHasher passwordHasher)
    : IPasswordRecoveryService
{
    // Checks the public input before asking persistence to perform the indivisible reset operation.
    public async Task ResetAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        string tokenHash = generator.ComputeHash(request.Token);
        PasswordResetToken? token = await tokens.GetByHashAsync(tokenHash, cancellationToken);
        if (token is null || !token.CanUseAt(timeProvider.GetUtcNow().UtcDateTime))
            throw new ArgumentException("Password reset token is invalid or expired.");
        passwordValidator.Validate(request.NewPassword);
        string passwordHash = passwordHasher.HashPassword(request.NewPassword);
        if (!await tokens.TryResetAsync(tokenHash, passwordHash, cancellationToken))
            throw new ArgumentException("Password reset token is invalid or expired.");
    }

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

### C:/Users/Hamza/Documents/OpsDesk API/src/OpsDesk.Application/Authorization/AuthClaimTypes.cs

```csharp
namespace OpsDesk.Application.Authorization;

public static class AuthClaimTypes
{
    public const string AuthVersion = "auth_version";
}
```

### C:/Users/Hamza/Documents/OpsDesk API/src/OpsDesk.Domain/Entities/User.cs

```csharp
using OpsDesk.Domain.Enums;

namespace OpsDesk.Domain.Entities;

public sealed class User
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public int AuthVersion { get; private set; }

    // Replaces the stored hash and advances the version carried by every authenticated session.
    public void ResetPassword(string passwordHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
        if (passwordHash.Length > 500) throw new ArgumentException("Password hash is too long.", nameof(passwordHash));
        int nextVersion = checked(AuthVersion + 1);
        PasswordHash = passwordHash;
        AuthVersion = nextVersion;
    }

    public UserRole Role { get; set; } = UserRole.Customer;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? EmailVerifiedAtUtc { get; private set; }

    public bool IsEmailVerified =>
        EmailVerifiedAtUtc.HasValue;

    /// <summary>
    /// Marks the User's email as verified and reports a state change.
    /// </summary>
    public bool MarkEmailVerified(DateTime verifiedAtUtc)
    {
        if (verifiedAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException(
                "Email verification time must be UTC.",
                nameof(verifiedAtUtc));
        }

        if (IsEmailVerified)
        {
            return false;
        }

        EmailVerifiedAtUtc = verifiedAtUtc;

        return true;
    }
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
    public DateTime? ConsumedAtUtc { get; private set; }

    private PasswordResetToken() { }

    // Rejects expired, revoked, consumed, or not-yet-valid credentials without changing state.
    public bool CanUseAt(DateTime nowUtc)
    {
        if (nowUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("Reset time must be UTC.", nameof(nowUtc));
        return RevokedAtUtc is null && ConsumedAtUtc is null && CreatedAtUtc <= nowUtc && nowUtc < ExpiresAtUtc;
    }

    // Marks a valid token used; persistence combines this with the password and session version update.
    public void Consume(DateTime nowUtc)
    {
        if (!CanUseAt(nowUtc)) throw new ArgumentException("Password reset token is invalid or expired.");
        ConsumedAtUtc = nowUtc;
    }

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
        return new GeneratedPasswordResetToken(rawToken, ComputeHash(rawToken));
    }

    // Only password-reset credentials are accepted; JWT, verification and invitation tokens are rejected.
    public string ComputeHash(string rawToken)
    {
        if (string.IsNullOrWhiteSpace(rawToken) || rawToken.Length != 47
            || !rawToken.StartsWith("pwd_", StringComparison.Ordinal)
            || rawToken[4..].Any(character => !char.IsAsciiLetterOrDigit(character) && character is not ('_' or '-')))
            throw new ArgumentException("Password reset token is invalid or expired.");
        return generator.ComputeHash(rawToken);
    }
}
```

### C:/Users/Hamza/Documents/OpsDesk API/src/OpsDesk.Infrastructure/Authentication/JwtTokenGenerator.cs

```csharp
using System;
using System.Collections.Generic;
using System.Globalization;
using OpsDesk.Application.Authorization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OpsDesk.Application.Auth.Interfaces;
using OpsDesk.Application.Auth.Models;
using OpsDesk.Domain.Entities;

namespace OpsDesk.Infrastructure.Authentication;

public sealed class JwtTokenGenerator : IJwtTokenGenerator
{
    private readonly JwtSettings _settings;

    public JwtTokenGenerator(IOptions<JwtSettings> options)
    {
        _settings = options.Value;

        ValidateSettings(_settings);
    }

    public JwtTokenResult GenerateToken(User user)
    {
        DateTime issuedAtUtc = DateTime.UtcNow;
        DateTime expiresAtUtc = issuedAtUtc.AddMinutes(
            _settings.ExpirationMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(
                JwtRegisteredClaimNames.Iat,
                new DateTimeOffset(issuedAtUtc)
                    .ToUnixTimeSeconds()
                    .ToString(),
                ClaimValueTypes.Integer64),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(AuthClaimTypes.AuthVersion, user.AuthVersion.ToString(CultureInfo.InvariantCulture), ClaimValueTypes.Integer32),
            new(
                ClaimTypes.Name,
                $"{user.FirstName} {user.LastName}".Trim()),
            new(ClaimTypes.Role, user.Role.ToString())
        };

        var securityKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_settings.SecretKey));

        var signingCredentials = new SigningCredentials(
            securityKey,
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            notBefore: issuedAtUtc,
            expires: expiresAtUtc,
            signingCredentials: signingCredentials);

        string accessToken =
            new JwtSecurityTokenHandler().WriteToken(token);

        return new JwtTokenResult(
            accessToken,
            expiresAtUtc);
    }

    private static void ValidateSettings(JwtSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.Issuer))
        {
            throw new InvalidOperationException(
                "JWT issuer is not configured.");
        }

        if (string.IsNullOrWhiteSpace(settings.Audience))
        {
            throw new InvalidOperationException(
                "JWT audience is not configured.");
        }

        if (Encoding.UTF8.GetByteCount(settings.SecretKey) < 32)
        {
            throw new InvalidOperationException(
                "JWT secret key must be at least 32 bytes.");
        }

        if (settings.ExpirationMinutes <= 0)
        {
            throw new InvalidOperationException(
                "JWT expiration must be greater than zero.");
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

public sealed class PasswordResetTokenRepository(OpsDeskDbContext database, TimeProvider timeProvider) : IPasswordResetTokenRepository
{
    // Avoids tracking during the preliminary check before expensive password hashing.
    public Task<PasswordResetToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        return database.PasswordResetTokens.AsNoTracking()
            .SingleOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);
    }

    // Uses the same account-first lock order as issuance; competing resets re-read the consumed state.
    public async Task<bool> TryResetAsync(string tokenHash, string newPasswordHash,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        Guid? userId = await database.PasswordResetTokens.Where(token => token.TokenHash == tokenHash)
            .Select(token => (Guid?)token.UserId).SingleOrDefaultAsync(cancellationToken);
        if (userId is null) return false;
        List<User> users = await database.Users.FromSqlInterpolated(
            $"SELECT * FROM users WHERE id = {userId.Value} FOR UPDATE").ToListAsync(cancellationToken);
        User? user = users.SingleOrDefault();
        if (user is null) return false;
        PasswordResetToken? token = await database.PasswordResetTokens
            .SingleOrDefaultAsync(row => row.TokenHash == tokenHash && row.UserId == user.Id, cancellationToken);
        DateTime nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        if (token is null || !token.CanUseAt(nowUtc)) return false;

        user.ResetPassword(newPasswordHash);
        token.Consume(nowUtc);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

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

    // Shares the account-first lock with reset and issuance, so revocation cannot race with consumption.
    public async Task RevokeAsync(Guid tokenId, DateTime revokedAtUtc, CancellationToken cancellationToken = default)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        Guid? userId = await database.PasswordResetTokens.Where(token => token.Id == tokenId)
            .Select(token => (Guid?)token.UserId).SingleOrDefaultAsync(cancellationToken);
        if (userId is null) return;
        List<User> users = await database.Users.FromSqlInterpolated(
            $"SELECT * FROM users WHERE id = {userId.Value} FOR UPDATE")
            .AsNoTracking().ToListAsync(cancellationToken);
        if (users.Count == 0) return;
        await database.PasswordResetTokens.Where(token => token.Id == tokenId && token.RevokedAtUtc == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(token => token.RevokedAtUtc, revokedAtUtc),
                cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
```

### C:/Users/Hamza/Documents/OpsDesk API/src/OpsDesk.Infrastructure/Persistence/Repositories/UserRepository.cs

```csharp
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using OpsDesk.Application.Auth.Interfaces;
using OpsDesk.Application.Common.Exceptions;
using OpsDesk.Domain.Entities;

namespace OpsDesk.Infrastructure.Persistence.Repositories;

public sealed class UserRepository : IUserRepository
{
    private readonly OpsDeskDbContext _dbContext;

    // Projects one scalar for JWT validation without loading the account's password hash.
    public Task<int?> GetAuthVersionAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return _dbContext.Users.Where(user => user.Id == userId)
            .Select(user => (int?)user.AuthVersion).SingleOrDefaultAsync(cancellationToken);
    }

    public UserRepository(OpsDeskDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<bool> ExistsByEmailAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.Users.AnyAsync(
            user => user.Email == email,
            cancellationToken);
    }

    public Task<User?> GetByEmailAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(
                user => user.Email == email,
                cancellationToken);
    }

    /// <summary>
    /// Retrieves one User as read-only data for identity and role checks.
    /// </summary>
    public Task<User?> GetByIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(
                user => user.Id == userId,
                cancellationToken);
    }

    /// <summary>
    /// Retrieves one User with EF tracking so its state can be persisted.
    /// </summary>
    public Task<User?> GetByIdForUpdateAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.Users.SingleOrDefaultAsync(
            user => user.Id == userId,
            cancellationToken);
    }

    public async Task AddAsync(
        User user,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.Users.AddAsync(
            user,
            cancellationToken);

        try
        {
            await _dbContext.SaveChangesAsync(
                cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: "ux_users_email"
            })
        {
            throw new ConflictException(
                "A user with this email address already exists.",
                exception);
        }
    }
}
```

### C:/Users/Hamza/Documents/OpsDesk API/src/OpsDesk.Infrastructure/Persistence/Configurations/UserConfiguration.cs

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpsDesk.Domain.Entities;

namespace OpsDesk.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(user => user.Id);
        builder.Property(user => user.AuthVersion).HasColumnName("auth_version").HasDefaultValue(0);

        builder.Property(user => user.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(user => user.FirstName)
            .HasColumnName("first_name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(user => user.LastName)
            .HasColumnName("last_name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(user => user.Email)
            .HasColumnName("email")
            .HasMaxLength(320)
            .IsRequired();

        builder.HasIndex(user => user.Email)
            .IsUnique()
            .HasDatabaseName("ux_users_email");

        builder.Property(user => user.PasswordHash)
            .HasColumnName("password_hash")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(user => user.Role)
            .HasColumnName("role")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(user => user.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(user => user.EmailVerifiedAtUtc)
            .HasColumnName("email_verified_at_utc")
            .HasColumnType("timestamp with time zone");
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
        builder.Property(token => token.ConsumedAtUtc).HasColumnName("consumed_at_utc");
        builder.HasIndex(token => token.UserId).IsUnique();
        builder.HasIndex(token => token.TokenHash).IsUnique();
        builder.HasOne<User>().WithMany().HasForeignKey(token => token.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
```

### C:/Users/Hamza/Documents/OpsDesk API/src/OpsDesk.Infrastructure/Persistence/Migrations/20260906185939_AddPasswordResetConsumptionAndAuthVersion.cs

```csharp
using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpsDesk.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPasswordResetConsumptionAndAuthVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "auth_version",
                table: "users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "consumed_at_utc",
                table: "password_reset_tokens",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "auth_version",
                table: "users");

            migrationBuilder.DropColumn(
                name: "consumed_at_utc",
                table: "password_reset_tokens");
        }
    }
}
```

### C:/Users/Hamza/Documents/OpsDesk API/src/OpsDesk.Api/Authorization/JwtSessionValidationEvents.cs

```csharp
using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using OpsDesk.Application.Auth.Interfaces;
using OpsDesk.Application.Authorization;

namespace OpsDesk.Api.Authorization;

public sealed class JwtSessionValidationEvents(IUserRepository users) : JwtBearerEvents
{
    // Runs after signature/issuer/audience/expiry validation and rejects revoked or legacy sessions.
    public override async Task TokenValidated(TokenValidatedContext context)
    {
        string[] identifiers = context.Principal?.FindAll(ClaimTypes.NameIdentifier).Select(claim => claim.Value).ToArray() ?? [];
        string[] versions = context.Principal?.FindAll(AuthClaimTypes.AuthVersion).Select(claim => claim.Value).ToArray() ?? [];
        if (identifiers.Length != 1 || versions.Length != 1
            || !Guid.TryParse(identifiers[0], out Guid userId)
            || !int.TryParse(versions[0], NumberStyles.None, CultureInfo.InvariantCulture, out int version))
        {
            context.Fail("Session is invalid. Sign in again.");
            return;
        }
        int? currentVersion = await users.GetAuthVersionAsync(userId, context.HttpContext.RequestAborted);
        if (currentVersion is null || currentVersion != version)
            context.Fail("Session is invalid. Sign in again.");
    }
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
    // Uses the reset credential instead of a JWT; successful reset does not automatically sign the user in.
    [AllowAnonymous]
    [HttpPost("reset-password")]
    [EnableRateLimiting(PasswordRecoveryRateLimiting.ResetPolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        await recovery.ResetAsync(request, cancellationToken);
        return NoContent();
    }

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
    public const string ResetPolicy = "password-recovery-reset";

    // Registers an endpoint-specific, per-IP limit; account existence never influences the partition.
    public static IServiceCollection AddPasswordRecoveryRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(_ => { });
        services.AddOptions<RateLimiterOptions>().Configure<IConfiguration>((options, configuration) =>
        {
            int permitLimit = configuration.GetValue("PasswordRecovery:RateLimits:RequestPermitLimit", 5);
            int resetLimit = configuration.GetValue("PasswordRecovery:RateLimits:ResetPermitLimit", 10);
            int windowMinutes = configuration.GetValue("PasswordRecovery:RateLimits:WindowMinutes", 15);
            if (permitLimit is < 1 or > 1000 || resetLimit is < 1 or > 1000 || windowMinutes is < 1 or > 1440)
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
            options.AddPolicy(ResetPolicy, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.MapToIPv6().ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = resetLimit,
                    Window = TimeSpan.FromMinutes(windowMinutes),
                    QueueLimit = 0,
                    AutoReplenishment = true
                }));
        });
        return services;
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

### C:/Users/Hamza/Documents/OpsDesk API/tests/OpsDesk.Tests/Integration/PasswordResetIntegrationTests.cs

```csharp
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpsDesk.Application.Auth.DTOs;
using OpsDesk.Application.Auth.Models;
using OpsDesk.Domain.Entities;
using OpsDesk.Infrastructure.Persistence;

namespace OpsDesk.Tests.Integration;

[Collection(IntegrationTestCollection.Name)]
public sealed class PasswordResetIntegrationTests(OpsDeskApiFixture fixture)
{
    // Failure after SQL writes but before transaction commit must roll back all three reset effects.
    [Fact]
    public async Task Failed_transaction_should_preserve_password_session_and_token_for_retry()
    {
        var failure = new FailResetSaveOnce();
        using var factory = fixture.Factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?> { ["PasswordRecovery:WorkerEnabled"] = "true" }));
            builder.ConfigureServices(services => services.ConfigureDbContext<OpsDeskDbContext>(
                options => options.AddInterceptors(failure)));
        });
        using HttpClient client = factory.CreateClient();
        var account = await RegisterAndRequestAsync(client);
        failure.UserId = account.Session.UserId;
        using HttpResponseMessage failed = await client.PostAsJsonAsync("/auth/reset-password",
            new ResetPasswordRequest(account.Message.RawToken, "ChangedPass!"));
        Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
        using HttpResponseMessage oldLogin = await client.PostAsJsonAsync("/auth/login", new LoginRequest(account.Email, "OriginalPass!"));
        Assert.Equal(HttpStatusCode.OK, oldLogin.StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", account.Session.AccessToken);
        using HttpResponseMessage me = await client.GetAsync("/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        using HttpResponseMessage retried = await client.PostAsJsonAsync("/auth/reset-password",
            new ResetPasswordRequest(account.Message.RawToken, "ChangedPass!"));
        Assert.Equal(HttpStatusCode.NoContent, retried.StatusCode);
    }

    private sealed class FailResetSaveOnce : SaveChangesInterceptor
    {
        private int _armed = 1;
        public Guid UserId { get; set; }

        // Injects one infrastructure failure after EF executed SQL, without replacing the real repository.
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
            CancellationToken cancellationToken = default)
        {
            bool resettingTarget = eventData.Context?.ChangeTracker.Entries<User>()
                .Any(entry => entry.Entity.Id == UserId && entry.Entity.AuthVersion > 0) == true;
            if (resettingTarget && Interlocked.Exchange(ref _armed, 0) == 1)
                throw new IOException("Simulated failure before reset transaction commit.");
            return ValueTask.FromResult(result);
        }
    }

    // Verification credentials and well-formed but unknown reset credentials cannot replace a password.
    [Fact]
    public async Task Wrong_purpose_and_unknown_tokens_should_preserve_account_access()
    {
        using var factory = CreateRecoveryFactory();
        using HttpClient client = factory.CreateClient();
        var account = await RegisterAndRequestAsync(client);
        EmailVerificationEmail verification = Assert.Single(fixture.Factory.SentEmails, message => message.RecipientEmail == account.Email);
        foreach (string token in new[] { verification.RawToken, "inv_" + new string('A', 43), "pwd_" + new string('A', 43), "bad-token" })
        {
            using HttpResponseMessage rejected = await client.PostAsJsonAsync("/auth/reset-password",
                new ResetPasswordRequest(token, "ChangedPass!"));
            Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        }
        using HttpResponseMessage confirmed = await client.PostAsJsonAsync("/auth/email-verification/confirm",
            new ConfirmEmailRequest(verification.RawToken));
        Assert.Equal(HttpStatusCode.NoContent, confirmed.StatusCode);
        using HttpResponseMessage reset = await client.PostAsJsonAsync("/auth/reset-password",
            new ResetPasswordRequest(account.Message.RawToken, "ChangedPass!"));
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        using HttpResponseMessage login = await client.PostAsJsonAsync("/auth/login", new LoginRequest(account.Email, "ChangedPass!"));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        AuthResponse current = Assert.IsType<AuthResponse>(await login.Content.ReadFromJsonAsync<AuthResponse>());
        Assert.Equal(account.Session.Role, current.Role);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", current.AccessToken);
        using HttpResponseMessage tickets = await client.GetAsync("/tickets");
        Assert.Equal(HttpStatusCode.OK, tickets.StatusCode);
    }

    // Reset limits are configurable and separate from the forgot-password request budget.
    [Fact]
    public async Task Reset_should_enforce_its_own_configured_ip_limit()
    {
        using var factory = fixture.Factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?> { ["PasswordRecovery:RateLimits:ResetPermitLimit"] = "2" })));
        using HttpClient client = factory.CreateClient();
        for (int attempt = 0; attempt < 2; attempt++)
        {
            using HttpResponseMessage response = await client.PostAsJsonAsync("/auth/reset-password", new ResetPasswordRequest("bad-token", "ChangedPass!"));
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        using HttpResponseMessage limited = await client.PostAsJsonAsync("/auth/reset-password", new ResetPasswordRequest("bad-token", "ChangedPass!"));
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        using HttpResponseMessage forgot = await client.PostAsJsonAsync("/auth/forgot-password", new { email = $"unknown-{Guid.NewGuid():N}@example.com" });
        Assert.Equal(HttpStatusCode.Accepted, forgot.StatusCode);
    }

    // The expiry boundary is exclusive; advancing a test clock avoids a thirty-minute wait.
    [Theory]
    [InlineData(1799, HttpStatusCode.NoContent)]
    [InlineData(1800, HttpStatusCode.BadRequest)]
    public async Task Reset_should_enforce_the_exact_expiration_boundary(int elapsedSeconds, HttpStatusCode expected)
    {
        var clock = new ResetClock(DateTimeOffset.UtcNow.AddMinutes(1));
        using var factory = CreateRecoveryFactory(clock);
        using HttpClient client = factory.CreateClient();
        var account = await RegisterAndRequestAsync(client);
        clock.Advance(TimeSpan.FromSeconds(elapsedSeconds));
        using HttpResponseMessage reset = await client.PostAsJsonAsync("/auth/reset-password",
            new ResetPasswordRequest(account.Message.RawToken, "ChangedPass!"));
        Assert.Equal(expected, reset.StatusCode);
        string expectedPassword = expected == HttpStatusCode.NoContent ? "ChangedPass!" : "OriginalPass!";
        using HttpResponseMessage login = await client.PostAsJsonAsync("/auth/login", new LoginRequest(account.Email, expectedPassword));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    // A newly delivered credential replaces the previous one without prematurely changing the password.
    [Fact]
    public async Task Replacement_token_should_reject_the_previous_token_and_allow_the_new_one()
    {
        var clock = new ResetClock(DateTimeOffset.UtcNow.AddMinutes(1));
        using var factory = CreateRecoveryFactory(clock);
        using HttpClient client = factory.CreateClient();
        var account = await RegisterAndRequestAsync(client);
        clock.Advance(TimeSpan.FromSeconds(60));
        using HttpResponseMessage forgot = await client.PostAsJsonAsync("/auth/forgot-password", new { email = account.Email });
        Assert.Equal(HttpStatusCode.Accepted, forgot.StatusCode);
        PasswordResetEmail replacement = await fixture.Factory.PasswordResetEmails.WaitForAsync(account.Email);
        using HttpResponseMessage old = await client.PostAsJsonAsync("/auth/reset-password",
            new ResetPasswordRequest(account.Message.RawToken, "ChangedPass!"));
        Assert.Equal(HttpStatusCode.BadRequest, old.StatusCode);
        using HttpResponseMessage current = await client.PostAsJsonAsync("/auth/reset-password",
            new ResetPasswordRequest(replacement.RawToken, "ChangedPass!"));
        Assert.Equal(HttpStatusCode.NoContent, current.StatusCode);
    }

    // Competing HTTP requests cannot both consume the same reset credential.
    [Fact]
    public async Task Concurrent_resets_should_have_exactly_one_winner()
    {
        using var factory = CreateRecoveryFactory();
        using HttpClient client = factory.CreateClient();
        var account = await RegisterAndRequestAsync(client);
        string[] passwords = ["FirstChanged!", "SecondChanged!"];
        Task<HttpResponseMessage>[] attempts = passwords.Select(password => client.PostAsJsonAsync("/auth/reset-password",
            new ResetPasswordRequest(account.Message.RawToken, password))).ToArray();
        HttpResponseMessage[] responses = await Task.WhenAll(attempts);
        try
        {
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.NoContent);
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.BadRequest);
            for (int index = 0; index < passwords.Length; index++)
            {
                using HttpResponseMessage login = await client.PostAsJsonAsync("/auth/login", new LoginRequest(account.Email, passwords[index]));
                Assert.Equal(responses[index].StatusCode == HttpStatusCode.NoContent ? HttpStatusCode.OK : HttpStatusCode.Unauthorized,
                    login.StatusCode);
            }
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", account.Session.AccessToken);
            using HttpResponseMessage me = await client.GetAsync("/me");
            Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
        }
        finally
        {
            foreach (HttpResponseMessage response in responses) response.Dispose();
        }
    }

    // Each isolated host gets its own request limiter while retaining the real PostgreSQL persistence.
    private WebApplicationFactory<Program> CreateRecoveryFactory(TimeProvider? clock = null)
    {
        return fixture.Factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?> { ["PasswordRecovery:WorkerEnabled"] = "true" }));
            if (clock is not null)
            {
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<TimeProvider>();
                    services.AddSingleton(clock);
                });
            }
        });
    }

    // Prepares a real account through HTTP and observes the email at the delivery boundary.
    private async Task<(string Email, AuthResponse Session, PasswordResetEmail Message)> RegisterAndRequestAsync(HttpClient client)
    {
        string email = $"reset-{Guid.NewGuid():N}@example.com";
        using HttpResponseMessage registration = await client.PostAsJsonAsync("/auth/register",
            new RegisterRequest("Reset", "User", email, "OriginalPass!"));
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
        AuthResponse session = Assert.IsType<AuthResponse>(await registration.Content.ReadFromJsonAsync<AuthResponse>());
        using HttpResponseMessage forgot = await client.PostAsJsonAsync("/auth/forgot-password", new { email });
        Assert.Equal(HttpStatusCode.Accepted, forgot.StatusCode);
        PasswordResetEmail message = await fixture.Factory.PasswordResetEmails.WaitForAsync(email);
        return (email, session, message);
    }

    private sealed class ResetClock(DateTimeOffset now) : TimeProvider
    {
        private long _ticks = now.UtcTicks;

        // Keeps application time deterministic and safe to read from the background delivery worker.
        public override DateTimeOffset GetUtcNow() => new(Interlocked.Read(ref _ticks), TimeSpan.Zero);

        // Moves only the test clock; no process-wide clock changes or arbitrary sleeps are needed.
        public void Advance(TimeSpan amount) => Interlocked.Add(ref _ticks, amount.Ticks);
    }

    // Validation failure preserves the credential so the user can correct the password and retry.
    [Fact]
    public async Task Invalid_new_password_should_not_consume_the_token_or_revoke_the_session()
    {
        using var factory = fixture.Factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?> { ["PasswordRecovery:WorkerEnabled"] = "true" })));
        using HttpClient client = factory.CreateClient();
        string email = $"validation-{Guid.NewGuid():N}@example.com";
        using HttpResponseMessage registration = await client.PostAsJsonAsync("/auth/register",
            new RegisterRequest("Reset", "User", email, "OriginalPass!"));
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
        AuthResponse original = Assert.IsType<AuthResponse>(await registration.Content.ReadFromJsonAsync<AuthResponse>());
        using HttpResponseMessage forgot = await client.PostAsJsonAsync("/auth/forgot-password", new { email });
        Assert.Equal(HttpStatusCode.Accepted, forgot.StatusCode);
        var delivered = await fixture.Factory.PasswordResetEmails.WaitForAsync(email);
        using HttpResponseMessage invalid = await client.PostAsJsonAsync("/auth/reset-password",
            new ResetPasswordRequest(delivered.RawToken, "short"));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", original.AccessToken);
        using HttpResponseMessage me = await client.GetAsync("/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        using HttpResponseMessage login = await client.PostAsJsonAsync("/auth/login", new LoginRequest(email, "OriginalPass!"));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        using HttpResponseMessage corrected = await client.PostAsJsonAsync("/auth/reset-password",
            new ResetPasswordRequest(delivered.RawToken, "ChangedPass!"));
        Assert.Equal(HttpStatusCode.NoContent, corrected.StatusCode);
    }

    // A reset changes the password, consumes the credential and rejects every pre-reset access token.
    [Fact]
    public async Task Reset_should_replace_password_consume_token_and_invalidate_existing_sessions()
    {
        using var factory = fixture.Factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?> { ["PasswordRecovery:WorkerEnabled"] = "true" })));
        using HttpClient client = factory.CreateClient();
        string email = $"reset-{Guid.NewGuid():N}@example.com";
        using HttpResponseMessage registration = await client.PostAsJsonAsync("/auth/register",
            new RegisterRequest("Reset", "User", email, "OriginalPass!"));
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
        AuthResponse original = Assert.IsType<AuthResponse>(await registration.Content.ReadFromJsonAsync<AuthResponse>());
        using HttpResponseMessage secondLogin = await client.PostAsJsonAsync("/auth/login", new LoginRequest(email, "OriginalPass!"));
        AuthResponse secondSession = Assert.IsType<AuthResponse>(await secondLogin.Content.ReadFromJsonAsync<AuthResponse>());
        using HttpResponseMessage forgot = await client.PostAsJsonAsync("/auth/forgot-password", new { email });
        Assert.Equal(HttpStatusCode.Accepted, forgot.StatusCode);
        var delivered = await fixture.Factory.PasswordResetEmails.WaitForAsync(email);

        var request = new { token = delivered.RawToken, newPassword = "ChangedPass!" };
        using HttpResponseMessage reset = await client.PostAsJsonAsync("/auth/reset-password", request);
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        using HttpResponseMessage replay = await client.PostAsJsonAsync("/auth/reset-password", request);
        Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);
        using HttpResponseMessage oldLogin = await client.PostAsJsonAsync("/auth/login", new LoginRequest(email, "OriginalPass!"));
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);
        foreach (string accessToken in new[] { original.AccessToken, secondSession.AccessToken })
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using HttpResponseMessage me = await client.GetAsync("/me");
            Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
        }
        client.DefaultRequestHeaders.Authorization = null;
        using HttpResponseMessage newLogin = await client.PostAsJsonAsync("/auth/login", new LoginRequest(email, "ChangedPass!"));
        Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);
        AuthResponse current = Assert.IsType<AuthResponse>(await newLogin.Content.ReadFromJsonAsync<AuthResponse>());
        Assert.Equal(original.UserId, current.UserId);
        Assert.Equal(original.Role, current.Role);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", current.AccessToken);
        using HttpResponseMessage currentMe = await client.GetAsync("/me");
        Assert.Equal(HttpStatusCode.OK, currentMe.StatusCode);
        using HttpResponseMessage tickets = await client.GetAsync("/tickets");
        Assert.Equal(HttpStatusCode.Forbidden, tickets.StatusCode);
    }
}
```

### C:/Users/Hamza/Documents/OpsDesk API/tests/OpsDesk.Tests/Integration/JwtSessionValidationIntegrationTests.cs

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OpsDesk.Application.Auth.DTOs;
using OpsDesk.Application.Authorization;
using OpsDesk.Infrastructure.Authentication;

namespace OpsDesk.Tests.Integration;

[Collection(IntegrationTestCollection.Name)]
public sealed class JwtSessionValidationIntegrationTests(OpsDeskApiFixture fixture)
{
    // Valid signatures alone cannot authorize legacy, malformed, duplicate, or mismatched session versions.
    [Theory]
    [InlineData(null, false)]
    [InlineData("invalid", false)]
    [InlineData("-1", false)]
    [InlineData("1", false)]
    [InlineData("0", true)]
    public async Task Invalid_session_versions_should_be_rejected_by_all_protected_routes(string? version, bool duplicate)
    {
        using HttpClient client = fixture.Factory.CreateClient();
        using HttpResponseMessage login = await client.PostAsJsonAsync("/auth/login",
            new LoginRequest(OpsDeskApiFactory.AdminEmail, OpsDeskApiFactory.AdminPassword));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        AuthResponse session = Assert.IsType<AuthResponse>(await login.Content.ReadFromJsonAsync<AuthResponse>());
        var reader = new JwtSecurityTokenHandler();
        List<Claim> claims = reader.ReadJwtToken(session.AccessToken).Claims
            .Where(claim => claim.Type != AuthClaimTypes.AuthVersion && claim.Type is not ("aud" or "iss" or "exp" or "nbf"))
            .ToList();
        if (version is not null) claims.Add(new Claim(AuthClaimTypes.AuthVersion, version));
        if (duplicate) claims.Add(new Claim(AuthClaimTypes.AuthVersion, version!));
        JwtSettings settings = fixture.Factory.Services.GetRequiredService<IOptions<JwtSettings>>().Value;
        var token = new JwtSecurityToken(settings.Issuer, settings.Audience, claims,
            DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(10),
            new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SecretKey)), SecurityAlgorithms.HmacSha256));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", reader.WriteToken(token));
        foreach (string route in new[] { "/me", "/admin/access", "/tickets" })
        {
            using HttpResponseMessage rejected = await client.GetAsync(route);
            Assert.Equal(HttpStatusCode.Unauthorized, rejected.StatusCode);
        }
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        using HttpResponseMessage valid = await client.GetAsync("/admin/access");
        Assert.Equal(HttpStatusCode.OK, valid.StatusCode);
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
        using HttpResponseMessage revoked = await client.PostAsJsonAsync("/auth/reset-password",
            new ResetPasswordRequest(sender.FailedToken, "ChangedPass!"));
        Assert.Equal(HttpStatusCode.BadRequest, revoked.StatusCode);
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

### C:/Users/Hamza/Documents/OpsDesk API/tests/OpsDesk.Tests/Auth/EmailVerificationServiceTests.cs

```csharp
using OpsDesk.Application.Auth.Interfaces;
using OpsDesk.Application.Auth.Models;
using OpsDesk.Application.Auth.Services;
using OpsDesk.Domain.Entities;
using OpsDesk.Infrastructure.Authentication;

namespace OpsDesk.Tests.Auth;

public sealed class EmailVerificationServiceTests
{
    [Fact]
    public async Task Issue_should_store_eight_hour_token_and_send_raw_value()
    {
        var now = new DateTimeOffset(
            2026,
            9,
            4,
            12,
            0,
            0,
            TimeSpan.Zero);
        var tokenRepository =
            new InMemoryEmailVerificationTokenRepository();
        var tokenGenerator =
            new EmailVerificationTokenGenerator();
        var emailSender =
            new RecordingEmailVerificationEmailSender();
        var timeProvider = new ManualTimeProvider(now);
        var userRepository = new InMemoryUserRepository();
        var service = new EmailVerificationService(
            userRepository,
            tokenRepository,
            tokenGenerator,
            emailSender,
            new EmailValidator(),
            timeProvider);
        User user = CreateUser();

        await service.IssueAsync(user);

        EmailVerificationToken? storedToken =
            await tokenRepository.GetByUserIdAsync(user.Id);
        EmailVerificationEmail sentEmail =
            Assert.Single(emailSender.SentEmails);

        Assert.NotNull(storedToken);
        Assert.Equal(now.UtcDateTime, storedToken.CreatedAtUtc);
        Assert.Equal(
            now.AddHours(8).UtcDateTime,
            storedToken.ExpiresAtUtc);
        Assert.Equal(user.Email, sentEmail.RecipientEmail);
        Assert.Equal(
            storedToken.TokenHash,
            tokenGenerator.ComputeHash(sentEmail.RawToken));
        Assert.Equal(
            storedToken.ExpiresAtUtc,
            sentEmail.ExpiresAtUtc);
    }

    [Fact]
    public async Task Resend_before_cooldown_should_keep_existing_token()
    {
        var now = new DateTimeOffset(
            2026,
            9,
            4,
            12,
            0,
            0,
            TimeSpan.Zero);
        var userRepository = new InMemoryUserRepository();
        var tokenRepository =
            new InMemoryEmailVerificationTokenRepository();
        var emailSender =
            new RecordingEmailVerificationEmailSender();
        var timeProvider = new ManualTimeProvider(now);
        var service = new EmailVerificationService(
            userRepository,
            tokenRepository,
            new EmailVerificationTokenGenerator(),
            emailSender,
            new EmailValidator(),
            timeProvider);
        User user = CreateUser();
        await userRepository.AddAsync(user);
        await service.IssueAsync(user);
        EmailVerificationToken originalToken =
            await tokenRepository.GetByUserIdAsync(user.Id)
            ?? throw new InvalidOperationException(
                "Initial token was not stored.");

        timeProvider.Advance(TimeSpan.FromSeconds(59));
        await service.ResendAsync(user.Email);

        EmailVerificationToken activeToken =
            await tokenRepository.GetByUserIdAsync(user.Id)
            ?? throw new InvalidOperationException(
                "Active token was not found.");

        Assert.Equal(originalToken.Id, activeToken.Id);
        Assert.Single(emailSender.SentEmails);
    }

    [Fact]
    public async Task Resend_at_cooldown_boundary_should_replace_token()
    {
        var now = new DateTimeOffset(
            2026,
            9,
            4,
            12,
            0,
            0,
            TimeSpan.Zero);
        var userRepository = new InMemoryUserRepository();
        var tokenRepository =
            new InMemoryEmailVerificationTokenRepository();
        var tokenGenerator =
            new EmailVerificationTokenGenerator();
        var emailSender =
            new RecordingEmailVerificationEmailSender();
        var timeProvider = new ManualTimeProvider(now);
        var service = new EmailVerificationService(
            userRepository,
            tokenRepository,
            tokenGenerator,
            emailSender,
            new EmailValidator(),
            timeProvider);
        User user = CreateUser();
        await userRepository.AddAsync(user);
        await service.IssueAsync(user);
        EmailVerificationToken originalToken =
            await tokenRepository.GetByUserIdAsync(user.Id)
            ?? throw new InvalidOperationException(
                "Initial token was not stored.");

        timeProvider.Advance(TimeSpan.FromSeconds(60));
        await service.ResendAsync(user.Email);

        EmailVerificationToken activeToken =
            await tokenRepository.GetByUserIdAsync(user.Id)
            ?? throw new InvalidOperationException(
                "Replacement token was not stored.");
        EmailVerificationEmail latestEmail =
            emailSender.SentEmails[^1];

        Assert.NotEqual(originalToken.Id, activeToken.Id);
        Assert.Equal(2, emailSender.SentEmails.Count);
        Assert.Equal(
            activeToken.TokenHash,
            tokenGenerator.ComputeHash(latestEmail.RawToken));
    }

    [Fact]
    public async Task Resend_for_unknown_email_should_not_send_email()
    {
        var emailSender =
            new RecordingEmailVerificationEmailSender();
        var service = new EmailVerificationService(
            new InMemoryUserRepository(),
            new InMemoryEmailVerificationTokenRepository(),
            new EmailVerificationTokenGenerator(),
            emailSender,
            new EmailValidator(),
            new ManualTimeProvider(DateTimeOffset.UtcNow));

        await service.ResendAsync("missing@example.com");

        Assert.Empty(emailSender.SentEmails);
    }

    [Fact]
    public async Task Resend_for_verified_user_should_not_send_email()
    {
        var now = new DateTimeOffset(
            2026,
            9,
            4,
            12,
            0,
            0,
            TimeSpan.Zero);
        var userRepository = new InMemoryUserRepository();
        var tokenRepository =
            new InMemoryEmailVerificationTokenRepository();
        var emailSender =
            new RecordingEmailVerificationEmailSender();
        var service = new EmailVerificationService(
            userRepository,
            tokenRepository,
            new EmailVerificationTokenGenerator(),
            emailSender,
            new EmailValidator(),
            new ManualTimeProvider(now));
        User user = CreateUser();
        user.MarkEmailVerified(now.UtcDateTime);
        await userRepository.AddAsync(user);

        await service.ResendAsync(user.Email);

        Assert.Null(
            await tokenRepository.GetByUserIdAsync(user.Id));
        Assert.Empty(emailSender.SentEmails);
    }

    [Fact]
    public async Task Confirm_with_valid_token_should_verify_user_and_consume_token()
    {
        var now = new DateTimeOffset(
            2026,
            9,
            4,
            12,
            0,
            0,
            TimeSpan.Zero);
        var userRepository = new InMemoryUserRepository();
        var tokenRepository =
            new InMemoryEmailVerificationTokenRepository();
        var emailSender =
            new RecordingEmailVerificationEmailSender();
        var timeProvider = new ManualTimeProvider(now);
        var service = new EmailVerificationService(
            userRepository,
            tokenRepository,
            new EmailVerificationTokenGenerator(),
            emailSender,
            new EmailValidator(),
            timeProvider);
        User user = CreateUser();
        await userRepository.AddAsync(user);
        await service.IssueAsync(user);
        string rawToken =
            Assert.Single(emailSender.SentEmails).RawToken;

        timeProvider.Advance(TimeSpan.FromMinutes(5));
        await service.ConfirmAsync(rawToken);

        Assert.True(user.IsEmailVerified);
        Assert.Equal(
            now.AddMinutes(5).UtcDateTime,
            user.EmailVerifiedAtUtc);
        Assert.Null(
            await tokenRepository.GetByUserIdAsync(user.Id));
    }

    [Fact]
    public async Task Confirm_at_expiration_time_should_reject_token()
    {
        var now = new DateTimeOffset(
            2026,
            9,
            4,
            12,
            0,
            0,
            TimeSpan.Zero);
        var userRepository = new InMemoryUserRepository();
        var tokenRepository =
            new InMemoryEmailVerificationTokenRepository();
        var emailSender =
            new RecordingEmailVerificationEmailSender();
        var timeProvider = new ManualTimeProvider(now);
        var service = new EmailVerificationService(
            userRepository,
            tokenRepository,
            new EmailVerificationTokenGenerator(),
            emailSender,
            new EmailValidator(),
            timeProvider);
        User user = CreateUser();
        await userRepository.AddAsync(user);
        await service.IssueAsync(user);
        string rawToken =
            Assert.Single(emailSender.SentEmails).RawToken;

        timeProvider.Advance(TimeSpan.FromHours(8));
        ArgumentException exception =
            await Assert.ThrowsAsync<ArgumentException>(
                () => service.ConfirmAsync(rawToken));

        Assert.Equal(
            "Email verification token is invalid or expired. " +
            "(Parameter 'rawToken')",
            exception.Message);
        Assert.False(user.IsEmailVerified);
    }

    [Fact]
    public async Task Confirm_with_unknown_token_should_return_generic_error()
    {
        var service = new EmailVerificationService(
            new InMemoryUserRepository(),
            new InMemoryEmailVerificationTokenRepository(),
            new EmailVerificationTokenGenerator(),
            new RecordingEmailVerificationEmailSender(),
            new EmailValidator(),
            new ManualTimeProvider(DateTimeOffset.UtcNow));

        ArgumentException exception =
            await Assert.ThrowsAsync<ArgumentException>(
                () => service.ConfirmAsync(
                    "not-a-real-verification-token"));

        Assert.Equal(
            "Email verification token is invalid or expired. " +
            "(Parameter 'rawToken')",
            exception.Message);
    }

    [Fact]
    public async Task Confirm_with_consumed_token_should_reject_replay()
    {
        var now = new DateTimeOffset(
            2026,
            9,
            4,
            12,
            0,
            0,
            TimeSpan.Zero);
        var userRepository = new InMemoryUserRepository();
        var tokenRepository =
            new InMemoryEmailVerificationTokenRepository();
        var emailSender =
            new RecordingEmailVerificationEmailSender();
        var service = new EmailVerificationService(
            userRepository,
            tokenRepository,
            new EmailVerificationTokenGenerator(),
            emailSender,
            new EmailValidator(),
            new ManualTimeProvider(now));
        User user = CreateUser();
        await userRepository.AddAsync(user);
        await service.IssueAsync(user);
        string rawToken =
            Assert.Single(emailSender.SentEmails).RawToken;
        await service.ConfirmAsync(rawToken);

        ArgumentException exception =
            await Assert.ThrowsAsync<ArgumentException>(
                () => service.ConfirmAsync(rawToken));

        Assert.Equal(
            "Email verification token is invalid or expired. " +
            "(Parameter 'rawToken')",
            exception.Message);
    }

    private static User CreateUser()
    {
        return new User
        {
            FirstName = "Email",
            LastName = "Verification",
            Email = "customer@example.com",
            PasswordHash = "not-a-real-password-hash"
        };
    }

    private sealed class InMemoryEmailVerificationTokenRepository :
        IEmailVerificationTokenRepository
    {
        private readonly List<EmailVerificationToken> _tokens = [];

        public Task<EmailVerificationToken?> GetByUserIdAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                _tokens.SingleOrDefault(
                    token => token.UserId == userId));
        }

        public Task<EmailVerificationToken?> GetByHashForUpdateAsync(
            string tokenHash,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                _tokens.SingleOrDefault(
                    token => token.TokenHash == tokenHash));
        }

        public Task ReplaceAsync(
            EmailVerificationToken token,
            CancellationToken cancellationToken = default)
        {
            _tokens.RemoveAll(
                existing => existing.UserId == token.UserId);
            _tokens.Add(token);

            return Task.CompletedTask;
        }

        public void Remove(EmailVerificationToken token)
        {
            _tokens.RemoveAll(
                existing => existing.Id == token.Id);
        }

        public Task SaveChangesAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryUserRepository : IUserRepository
    {
        private readonly List<User> _users = [];

        // Implements the current user-store contract for these verification-only tests.
        public Task<int?> GetAuthVersionAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_users.SingleOrDefault(user => user.Id == userId)?.AuthVersion);
        }

        public Task<bool> ExistsByEmailAsync(
            string email,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                _users.Any(user => user.Email == email));
        }

        public Task<User?> GetByEmailAsync(
            string email,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                _users.SingleOrDefault(
                    user => user.Email == email));
        }

        public Task<User?> GetByIdAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                _users.SingleOrDefault(
                    user => user.Id == userId));
        }

        public Task<User?> GetByIdForUpdateAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                _users.SingleOrDefault(
                    user => user.Id == userId));
        }

        public Task AddAsync(
            User user,
            CancellationToken cancellationToken = default)
        {
            _users.Add(user);

            return Task.CompletedTask;
        }
    }

    private sealed class RecordingEmailVerificationEmailSender :
        IEmailVerificationEmailSender
    {
        public List<EmailVerificationEmail> SentEmails { get; } = [];

        public Task SendAsync(
            EmailVerificationEmail email,
            CancellationToken cancellationToken = default)
        {
            SentEmails.Add(email);

            return Task.CompletedTask;
        }
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow;

        public ManualTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow()
        {
            return _utcNow;
        }

        public void Advance(TimeSpan duration)
        {
            _utcNow = _utcNow.Add(duration);
        }
    }
}
```
