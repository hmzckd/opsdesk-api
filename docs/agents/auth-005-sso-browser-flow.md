# AUTH-005: OIDC Browser Sign-in and Logout

## Durum ve Sinir

Bu belge AUTH-005'in ikinci dikey dilimini aciklar: OIDC ayarlari, tarayici challenge/callback akisi,
yerel Keycloak kurulumu ve logout. Ilk dilimdeki hesap esleme ve atomik davet kabul kurallari
`auth-005-sso-account-provisioning.md` dosyasindadir.

SSO varsayilan olarak kapalidir. Keycloak `start-dev` yalnizca yerel gelistirme icindir.
Kalici gelistirme veritabani bu dilimde migration ile guncellenmedi. Git commit, push ve tag islemleri
kullanicinin istegiyle sona birakildi.

## Task'in Amaci

Kurumsal kimligi OIDC ile dogrulanan ve OpsDesk daveti bulunan kisi, yerel parola olusturmadan
OpsDesk JWT'si alabilsin; cikista hem yerel JWT'ler iptal edilsin hem de saglayici oturumunu kapatacak
ayri tarayici adimi verilsin.

## Temel Terimler

- OIDC (OpenID Connect): OpsDesk'in parolayi gormeden baska bir kimlik saglayicinin kullaniciyi
  dogruladigina guvenmesini saglayan standart protokol.
- Authorization code: Saglayicinin callback'e kisa omurlu olarak getirdigi tek kullanimlik kod.
  OpsDesk bu kodu arka kanalda token ile degistirir.
- PKCE: Kodu baslatan OpsDesk oturumu ile geri donen kodu birbirine baglayan ek sir kontrolu.
  Proje `S256` kullanir.
- State: Giris isleminin OpsDesk tarafindan baslatildigini kanitlayan, imzali ve korumali durum.
- Nonce: Eski bir kimlik tokeninin yeniden oynatilmasini engelleyen tek kullanimlik deger.
- Callback: Saglayicinin basarili giristen sonra tarayiciyi dondurdugu `/signin-oidc` adresi.
- Scheme: ASP.NET Core icinde hangi kimlik dogrulama mekanizmasinin calisacagini belirleyen addir.
- Anti-forgery: Baska bir sitenin kullanicinin tarayicisindan habersizce SSO baslatmasini engelleyen
  eslesmis cookie ve form tokeni.
- Options pattern: `appsettings`, environment variable ve User Secrets degerlerini tipli bir C#
  nesnesi olarak DI'dan okumak.
- AuthVersion: JWT icindeki oturum surumu. Veritabanindaki surum artinca eski JWT gecersiz olur.

## HTTP Akisi

```text
Browser -> GET /auth/sso/login
Browser -> POST /auth/sso/login (anti-forgery + optional invitation code)
OpsDesk -> Keycloak /authorize (code + PKCE, protected state)
Keycloak -> GET /signin-oidc (code + state)
OpsDesk OIDC handler -> token endpoint -> signed ID token validation
SsoOpenIdConnectEvents -> IExternalSignInService -> PostgreSQL
OpsDesk -> AuthResponse containing the local JWT
```

Ilk giriste davet kodunun kendisi provider URL'sine konmaz. Kodun tek yonlu hash'i, ASP.NET Core'un
Data Protection sistemiyle korunan `AuthenticationProperties.Items` icinde state'e girer. Callback'te
OIDC handler imza, issuer, audience, expiry, nonce, state ve correlation cookie kontrollerini bitirmeden
`VerifiedExternalIdentity` uretilmez.

## Katmanlar ve Dosyalar

- `src/OpsDesk.Api/Configuration/SsoSettings.cs`: host ayarlarinin tipli sekli.
- `src/OpsDesk.Api/Configuration/SsoSettingsValidator.cs`: eksik veya guvensiz ayarla baslatmayi reddeder.
- `src/OpsDesk.Api/Configuration/SsoConfiguration.cs`: Options, DI, gecici cookie ve OIDC handler kayitlari.
- `src/OpsDesk.Api/Authentication/SsoAuthenticationSchemes.cs`: scheme ve korumali-state anahtarlari.
- `src/OpsDesk.Api/Authentication/SsoOpenIdConnectEvents.cs`: dogrulanmis OIDC olaylarini OpsDesk akisine cevirir.
- `src/OpsDesk.Api/Authentication/SsoProviderLogoutUrlFactory.cs`: discovery belgesinden logout URL'si kurar.
- `src/OpsDesk.Api/Controllers/SsoController.cs`: tarayici baslangic formu ve yerel logout HTTP siniri.
- `src/OpsDesk.Api/Models/SsoLogoutResponse.cs`: logout cevabinin guvenli API sozlesmesi.
- `src/OpsDesk.Application/Auth/Services/AuthSessionService.cs`: tum yerel JWT'leri iptal etme use case'i.
- `src/OpsDesk.Infrastructure/Persistence/Repositories/UserRepository.cs`: AuthVersion'i atomik SQL ile arttirir.
- `deploy/keycloak/opsdesk-realm.json`: yerel realm, confidential client ve demo kullanici importu.
- `scripts/setup-local-sso.ps1`: rastgele yerel secret uretir, `.env` ve User Secrets'i birbiriyle esler.
- `tests/OpsDesk.Tests/Integration/ExternalSignInIntegrationTests.cs`: HTTP challenge/callback/logout davranislari.
- `tests/OpsDesk.Tests/Configuration/SsoSettingsValidatorTests.cs`: startup ayar kurallari.

## Metotlari Okuma

### SsoConfiguration.AddSsoConfiguration - Api

`public static IServiceCollection AddSsoConfiguration(this IServiceCollection services)`

`public` baska siniflarin cagirabilecegini, `static` once nesne olusturulmadigini anlatir.
`this IServiceCollection` bu metodu `builder.Services.AddSsoConfiguration()` biciminde kullanilan bir
extension method yapar. Parametre mevcut DI servis listesidir; donen `IServiceCollection` ayni listenin
zincirlenebilmesini saglar. Govde SsoSettings binding/validation, anti-forgery, event sinifi, gecici
cookie ve resmi OIDC handler'i kaydeder. API katmanindadir cunku ASP.NET Core host ve HTTP kimlik
dogrulama ayrintilarini bilir; Application bu ayrintilari bilmez.

### SsoSettingsValidator.Validate - Api

`public ValidateOptionsResult Validate(string? name, SsoSettings settings)`

`string? name` .NET Options altyapisinin gonderebildigi opsiyonel ayar adidir; bu projede named option
kullanilmadigi icin govdede gerekmez. `settings` dogrulanacak tipli ayarlardir. Metot SSO kapaliysa
basarili olur; aciksa authority, public origin, client bilgisi ve exact domain listesini kontrol eder.
Production'da veya internet adreslerinde HTTP'yi reddeder. `ValidateOptionsResult` baslangicin devam
edip edemeyecegini tasir. Ayar hatasini ilk kullanici isteginde degil uygulama acilirken bulmak icin
`ValidateOnStart` ile birlikte calisir.

Yardimcilar `ValidateAbsoluteUri`, `ValidateTransport` ve `IsValidDomain` private'dir: sadece validator
icindeki ortak kontrolleri bolerler ve disariya yeni API sozlesmesi acmazlar.

### SsoController.Start - Api

`public IActionResult Start()`

Parametre almaz; mevcut HTTP istegi `ControllerBase.HttpContext` uzerinden erisilir. SSO kapaliysa 404,
aciksa HTML formu dondurur. `IActionResult`, cevabin HTML veya 404 gibi birden fazla HTTP biciminden biri
olabilecegini belirtir. Anti-forgery cookie/token ciftini burada olusturur. Provider'a henuz baglanmaz.

### SsoController.BeginSignIn - Api

`public async Task<IActionResult> BeginSignIn(string? invitationCode, CancellationToken cancellationToken)`

`async Task<IActionResult>`, asenkron is bittiginde bir HTTP sonucu dondurur; `IActionResult` parametre
degildir. Nullable `invitationCode`, daha once baglanmis kullanicinin tekrar girisinde davete ihtiyac
olmadigi icin bos olabilir. `CancellationToken` tarayici baglantiyi keserse iptal sinyalini tasir.
Govde anti-forgery eslesmesini kontrol eder, varsa ham daveti yalnizca hash'e cevirir ve korumali state'e
koyar, sonra named OIDC scheme ile `Challenge` dondurur. Challenge 401 demek degildir; burada tarayiciyi
Keycloak'a yonlendiren authentication eylemidir.

### SsoController.Logout - Api

`public async Task<ActionResult<SsoLogoutResponse>> Logout(CancellationToken cancellationToken)`

`ActionResult<T>`, basarida tipli `SsoLogoutResponse`, hatada 401 gibi HTTP sonucu dondurebilir.
Metot request body'sinden user ID almaz; JWT'nin `NameIdentifier` claim'ini Guid'e cevirir. Application
servisiyle AuthVersion'i arttirip tum yerel JWT'leri iptal eder, sonra provider logout URL'sini uretir.
URL uretilemese bile yerel iptal geri alinmaz. Controller'dadir cunku claim ve HTTP cevabi bilir.

### SsoController.SignedOut - Api

`public IActionResult SignedOut()`

Keycloak tarayiciyi geri dondurdugunde sabit ve zararsiz bir HTML sayfasi verir. SSO kapaliysa 404'tur.
Provider logout callback'i veritabani yazmaz; yerel JWT iptali daha once POST logout'ta yapilmistir.

### SsoOpenIdConnectEvents.RedirectToIdentityProvider - Api

`public override Task RedirectToIdentityProvider(RedirectContext context)`

`override`, resmi `OpenIdConnectEvents` taban sinifindaki olayi bu proje icin ozellestirir. `context`
handler'in kurdugu provider mesajini tasir. Metot callback URI'yi istekten gelen ve taklit edilebilen
Host header yerine dogrulanmis `PublicOrigin` ayarindan kurar. I/O yapmadigi icin tamamlanmis bir
`Task` dondurur.

### SsoOpenIdConnectEvents.TokenValidated - Api

`public override Task TokenValidated(TokenValidatedContext context)`

Bu olay ancak handler ID tokenin imzasini ve protokol kontrollerini gectikten sonra calisir. Gercek
`SecurityToken.Issuer` degerini korumali authentication state'e yazar. Issuer'i istemciden veya e-posta
claim'inden tahmin etmez.

### SsoOpenIdConnectEvents.TicketReceived - Api

`public override async Task TicketReceived(TicketReceivedContext context)`

Provider cookie'si olusturmak yerine dogrulanmis claim'leri `VerifiedExternalIdentity` girdisine cevirir,
Application servisini cagirir ve normal OpsDesk `AuthResponse` JSON'unu yazar. `context.HandleResponse()`
standart cookie/redirect devam akisinin bu cevap icin durduruldugunu bildirir. `no-store`, `no-cache` ve
`no-referrer` header'lari token cevabinin cache veya referrer yoluyla sizmasini azaltir.

### SsoOpenIdConnectEvents.RemoteFailure - Api

`public override async Task RemoteFailure(RemoteFailureContext context)`

Bozuk state, gecersiz code veya provider protokol hatalarini generic 401 Problem Details cevabina cevirir.
Loga token, claim, e-posta veya exception mesaji degil sadece exception turu yazilir. Bu nedenle provider
ayrintisi dis istemciye sizmaz.

### SsoProviderLogoutUrlFactory.CreateAsync - Api

`public async Task<string?> CreateAsync(CancellationToken cancellationToken = default)`

`Task<string?>`, asenkron discovery sonunda URL veya `null` dondurur. `CancellationToken = default`
parametrenin verilmemesine izin verir. `IOptionsMonitor<OpenIdConnectOptions>` named OIDC handler'in
hazir ayarini okur. Discovery'deki `end_session_endpoint` ile sabit `client_id` ve exact
`post_logout_redirect_uri` parametrelerini kurar. Provider ulasilamazsa local logout'u bozmak yerine null
dondurur. Provider protokolunu bildigi icin Api katmanindadir.

### AuthSessionService.RevokeAllAsync - Application

`public async Task RevokeAllAsync(Guid userId, CancellationToken cancellationToken = default)`

`Task` burada sonuc nesnesi olmadigini, fakat islemin asenkron tamamlandigini belirtir. `userId` JWT'den
gelir; body'den alinmaz. Repository bir satiri guncelleyemezse hesap artik yokmus gibi 401 akisi baslatir.
Hangi islemle tum oturumlarin iptal edilecegini yonettigi icin Application katmanindadir; SQL bilmez.

### UserRepository.RevokeSessionsAsync - Infrastructure

`public async Task<bool> RevokeSessionsAsync(Guid userId, CancellationToken cancellationToken = default)`

`bool` tam bir kullanici satirinin guncellenip guncellenmedigini bildirir. `ExecuteUpdateAsync` entity'yi
once bellekte yuklemeden `auth_version = auth_version + 1` SQL'i calistirir. Tek atomik veritabani islemi,
iki es zamanli logout/reset isteginin birbirinin artislarini ezmesini engeller. EF Core ve SQL ayrintisi
bildigi icin Infrastructure katmanindadir.

### User.RevokeSessions - Domain

`public void RevokeSessions()`

`void`, metodun bir sonuc nesnesi dondurmedigini anlatir. Parametre almaz; kendi `AuthVersion` degerini
bir arttirir. Bu kucuk Domain davranisi bellek ici repository kullanan unit testlerde ve entity uzerinden
calisan akislarda ayni kurali korur. Gercek PostgreSQL logout'u performans ve eszamanlilik icin yukaridaki
atomik repository metodunu kullanir.

### Private ve Yerel Yardimcilar

- `SsoOpenIdConnectEvents.BuildPublicUri(string path)`: ayarlanan origin ile guvenilen callback yolunu
  birlestirir. `string` alir ve tam `string` URL dondurur.
- `GetRequiredClaim(ClaimsPrincipal principal, string claimType)`: dogrulanmis principal icinde zorunlu
  claim'i bulur; yoksa girisi durdurur. `static` olmasi nesne durumuna ihtiyac duymadigini gosterir.
- `GetRequiredItem(AuthenticationProperties properties, string key)`: korumali state icindeki zorunlu
  degeri okur; eksik state'i kabul etmez.
- `SsoSettingsValidator.ValidateAbsoluteUri(...)`: metni HTTP/HTTPS `Uri` nesnesine cevirir ve hata
  listesini doldurur. `Uri?` donusu gecersiz girdide null olabilecegini anlatir.
- `SsoSettingsValidator.ValidateTransport(...)`: HTTP'nin sadece yerel Development/Testing loopback
  adresinde kullanilmasina izin verir; sonuc yerine hata listesi uzerinde yan etki yapar.
- `SsoSettingsValidator.IsValidDomain(string domain)`: wildcard kullanmadan ASCII domain label
  kurallarini kontrol eden saf bool yardimcisidir.
- PowerShell `New-UrlSafeSecret`: kriptografik rastgele byte uretip URL-safe Base64 secret dondurur.
- PowerShell `Get-EnvironmentValue` ve `Set-EnvironmentValue`: yalnizca `.env` icindeki exact anahtari
  okur veya gunceller; mevcut ilgisiz satirlari korur.
- PowerShell `Get-OrCreateSecret`: mevcut guclu degeri yeniden kullanir, bos veya `change-me` degerini
  rastgele secret ile degistirir; scriptin tekrar calistirilmasini idempotent yapar.

`SsoLogoutResponse(bool LocalSessionsRevoked, string? ProviderLogoutUrl)` bir `record`dur. Metot degildir;
logout cevabindaki iki degeri adlariyla tasiyan immutable DTO'dur. Ikinci alan nullable'dir, cunku provider
discovery gecici olarak ulasilamazken yerel oturum iptali yine basarili olabilir.

## Test Sinirlari ve TDD Kaniti

HTTP testleri gercek Controller, ASP.NET Core OIDC handler, Application servisleri, EF Core migrationlari
ve gecici PostgreSQL kullanir. Yalnizca harici provider'in token endpoint'i test icinde sahte RSA imzali
cevapla sinirlandirilir. Bu, OpsDesk kodunu mocklamak degil dis sistem sinirini kontrollu tutmaktir.

- Baslangic sayfasi testi once 404 idi; GET endpoint'i eklenince yesil oldu.
- Form challenge testi once 405 idi. Anti-forgery servis dogrulamasi ve static test discovery bilgisiyle
  code + PKCE redirect'i yesil oldu.
- Callback testi correlation cookie'nin yanlis HTTPS karari yuzunden once 401 idi. Authority ile
  PublicOrigin tasima kurallari ayrilinca signed callback ve olusan JWT yesil oldu.
- Logout testi endpoint yokken 404 idi. AuthVersion iptali ve provider URL factory eklenince yesil oldu.
- Ayrica SSO kapali, anti-forgery eksik ve bilinmeyen state senaryolari reddedilir.

Test yardimcilari `CreateTransportFactory` ve `CreateCallbackFactory`, yalnizca o test hostuna SSO ayari
ve kontrollu provider metadata'si verir. `FakeOidcBackchannel.SendAsync`, token endpoint sinirinda RSA
imzali bir ID token uretir; Controller, handler, Application ve PostgreSQL'i sahte hale getirmez.
`InviteAsync` gercek admin HTTP akisini kullanir; `SignInAsync` ise hesap-provisioning davranisini ayri
DI scope'larinda calistirarak paylasilan DbContext yanilgisini engeller.

Dar SSO paketi 21/21 gecti. Tam cozum 324/324 gecti; atlanan test yok. Build 0 hata ve 0 uyariyla
tamamlandi. Slopwatch strict taramasi 0 sorun buldu.

## Yerel Keycloak

Kurulum scripti var olan `.env` degerlerini korur; eksik veya `change-me` olan SSO secret'larini guvenli
rastgele degerlerle doldurur. Client secret'in aynisini .NET User Secrets'e yazar. Degerleri ekrana basmaz.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/setup-local-sso.ps1
```

```powershell
docker compose -f docker-compose.yml -f docker-compose.sso.yml --profile sso up -d postgres mailpit keycloak
```

Keycloak discovery kontrolunde issuer, authorization, token ve logout endpoint'leri `opsdesk` realm'ine
ait geldi; `S256` PKCE destekleniyor. Realm importu mevcut named volume'da ayni realm varsa tekrar
uygulanmaz. Production'da `start-dev`, committed demo user ve yerel secret'lar kullanilmaz.

Gercek yerel browser kontrolu kalici gelistirme veritabani yerine `--rm` gecici PostgreSQL ile yapildi.
Admin HTTP girisi daveti olusturdu, Mailpit e-postayi yakaladi, Keycloak demo kullaniciyi dogruladi ve
callback ayni transaction'da passwordless, verified Agent hesabi ile issuer/subject linkini kaydetti.
Sonraki provider callback'leri ayni linki buldu. Gercek logout kontrolunde `/me` once 200, AuthVersion
artisindan sonra ayni JWT ile 401 oldu; Keycloak logout onayindan sonra
`/auth/sso/signed-out` sayfasina donuldu. In-app browser ilk callback'in JSON ekranini gorsel olarak
yenilemedi; bu nedenle JWT cevap sozlesmesi ayrica gercek OIDC handler kullanan otomatik HTTP testiyle
kanitlandi ve browser kaniti oldugundan fazla genisletilmedi.

## Guvenlik Kararlari

- SSO kapali varsayilanla baslar; yarim konfigurasyon uygulamayi startup'ta durdurur.
- HTTP sadece Development/Testing icinde loopback Authority/PublicOrigin icin kabul edilir.
- Client secret source code'a, provider redirect URL'sine veya loga yazilmaz.
- Callback adresi Host header'dan degil sabit PublicOrigin'den kurulur.
- Provider access token saklanmaz ve OpsDesk API tokeni olarak kullanilmaz.
- Provider rol claim'leri kullanilmaz; OpsDesk rolu yonetici davetinden gelir.
- Provider logout URL'sinde OpsDesk JWT'si bulunmaz.
- Local logout her mevcut JWT'yi iptal eder; provider logout ayri ve acik bir tarayici adimidir.
