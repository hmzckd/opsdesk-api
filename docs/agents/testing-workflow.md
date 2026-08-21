# OpsDesk BDD and TDD Workflow

Bu belge, Asana'dan alinan bir OpsDesk gorevinin davranis senaryosundan calisan koda nasil donusturulecegini tanimlar.

## 1. Temel Kavramlar

- **BDD (Behavior-Driven Development):** Kullanici veya sistem acisindan hangi davranisin beklendigini somut orneklerle belirler.
- **Gherkin:** BDD orneklerini `Given / When / Then` biciminde yazmak icin kullanilan okunabilir dildir.
- **TDD (Test-Driven Development):** Bir davranis icin once basarisiz testin yazildigi, sonra testi gecirecek en kucuk kodun eklendigi gelistirme dongusudur.
- **Public seam:** Davranisin disaridan gozlemlendigi sinirdir. OpsDesk icin bu sinir genellikle bir HTTP endpoint'i veya onaylanmis bir Application arayuzudur.
- **Acceptance test:** Bir davranisin kullaniciya sunulan dis sinirdan calistigini dogrulayan testtir.

BDD neyi insa edecegimizi, TDD ise bu davranisi guvenle nasil gelistirecegimizi belirler.

## 2. Kaynaklarin Yeri

- Gherkin senaryolari ilgili Asana task'inin `Gherkin Scenarios` bolumunde tutulur.
- Calisan otomatik testler xUnit test projelerinde tutulur.
- Unit ve integration testleri `tests/OpsDesk.Tests` altinda bulunur.
- `.feature` dosyalari veya Reqnroll simdilik kullanilmaz. Executable Gherkin ihtiyaci dogarsa ayri bir `SPIKE` task'i ile degerlendirilir.

Asana senaryosu okunabilir davranis sozlesmesidir. xUnit testi bu sozlesmenin calisan dogrulamasidir.

## 3. Asana Task Zorunlu Alanlari

Her uygulanabilir task su bolumleri icermelidir:

- `Outcome`: Kullanici acisindan teslim edilecek sonuc.
- `Affected Layers`: Domain, Application, Infrastructure, API ve Tests kapsamindan etkilenenler.
- `In Scope`: Bu task'ta yapilacaklar.
- `Out of Scope`: Bilerek sonraya birakilanlar.
- `Public Seam`: Davranisin test edilecegi dis sinir.
- `Dependencies`: Once tamamlanmasi gereken task'lar.
- `Gherkin Scenarios`: Is acisindan onemli 1-3 kabul senaryosu.
- `Test Plan`: Gerekli unit, integration ve acceptance testleri.
- `Definition of Done`: Task'in tamamlanmis sayilma kosullari.

Bir task'in `Ready` bolumunde bulunmasi dosya degisikligi izni vermez. Uygulama baslamadan once `AGENTS.md` izin kurallari uygulanir.

## 4. Task Fetch Akisi

1. Once Asana `In Progress` bolumu okunur.
2. Bir task varsa yeni task secilmez; mevcut task surdurulur.
3. `In Progress` bos ise `Ready` bolumundeki tamamlanmis bagimliliklara sahip en yuksek oncelikli task secilir.
4. Task'in aciklamasi, bagimliliklari, yorumlari ve kabul kriterleri tamamen okunur.
5. Amac, etkilenecek katmanlar, dosyalar, public seam ve ilk Gherkin senaryosu kullaniciya aciklanir.
6. Kullanici uygulamayi onayladiktan sonra task ve kod calismasi baslatilir.
7. Sonuc test kanitlariyla `Review` asamasinda sunulur.
8. Asana durum degisikligi, Git commit, push ve tag islemleri ilgili acik izinlere tabidir.

## 5. Gherkin Yazim Kurallari

- Senaryo kullanici veya is diliyle yazilir.
- `Given` baslangic durumunu tanimlar.
- `When` tek ana eylemi tanimlar.
- `Then` disaridan gozlemlenebilen sonucu tanimlar.
- Sinif, metot, repository, tablo veya mock adi kullanilmaz.
- Bir senaryo mumkun oldugunca 3-5 adimda tutulur.
- Her teknik edge case Gherkin'e tasinmaz. Is acisindan onemli ornekler Gherkin, ayrintili kombinasyonlar xUnit unit testleri olur.

Ornek:

```gherkin
Feature: Ticket creation

  Rule: Authenticated Customers can create support tickets

    Scenario: Customer creates a valid ticket
      Given an authenticated Customer
      When the Customer creates a ticket with valid required fields
      Then the API returns 201 Created
      And the ticket has Open status
      And the Customer is identified as the requester
```

Kotu ornek:

```gherkin
Scenario: Ticket service calls the repository
  When CreateTicketAsync calls AddAsync
  Then AddAsync should be called once
```

Kotu ornek kullanicinin gordugu davranisi degil, mevcut implementasyonun ic yapisini tarif eder.

## 6. Dikey BDD ve TDD Dongusu

Her task icin tum testleri ve tum kodu toplu yazmak yerine su dongu tekrarlanir:

1. Task'tan is acisindan en kucuk anlamli Gherkin senaryosu secilir.
2. Test edilecek public seam ve beklenen sonuc onaylanir.
3. Yalnizca bu senaryoyu temsil eden xUnit testi yazilir.
4. Test calistirilir ve beklenen nedenle basarisiz oldugu dogrulanir: **Red**.
5. Gerekiyorsa Domain kurali icin daha kucuk bir unit test yazilir.
6. Testi gecirecek en kucuk production kodu yazilir.
7. Ilgili test yeniden calistirilir ve gectigi dogrulanir: **Green**.
8. Ayni islem task'in siradaki senaryosu icin tekrarlanir.
9. Davranis testleri gectikten sonra okunabilirlik ve tasarim review edilir: **Refactor**.
10. Tam test paketi ve build calistirilir.

Bu yaklasimda kod teste degil, onceden kararlastirilmis davranisa baglanir.

## 7. Test Seviyeleri

### Unit Tests

Hizli ve izole is kurallarini dogrular.

OpsDesk ornekleri:

- Ticket durum gecisleri
- Priority ve status kurallari
- Input validation
- Rol bazli karar fonksiyonlari

### Integration Tests

Birden fazla gercek bilesenin birlikte calistigini dogrular.

OpsDesk ornekleri:

- EF Core ve PostgreSQL davranisi
- Unique ve foreign key constraint'leri
- Migration'lar
- Repository sorgulari

### API Acceptance Tests

Uygulamayi disaridan HTTP uzerinden dogrular.

OpsDesk ornekleri:

- `POST /tickets` sonucunun `201 Created` dondurmesi
- Customer'in yalnizca gorebildigi ticket'a erismesi
- Yetkisiz status gecisinin reddedilmesi
- Olusturulan ticket'in sonraki API istegiyle goruntulenebilmesi

API acceptance testleri `HttpClient`, `WebApplicationFactory` ve gerekli oldugunda PostgreSQL Testcontainers kullanir.

## 8. Coupling Kurallari

Istenen contract coupling:

- HTTP route
- HTTP status code
- Request ve response contract'i
- Kullaniciya gorunen ticket status'u
- Rol ve yetki sonucu

Kacinilacak implementation coupling:

- Private metotlari test etmek
- Bir repository metodunun kac kez cagrildigini dogrulamak
- Dahili metot cagri sirasini sabitlemek
- Ayni davranis korunurken refactoring nedeniyle testi kirmak
- Acceptance testinde sonucu yalnizca dogrudan tablo sorgusuyla kontrol etmek

Bir refactoring public davranisi degistirmiyorsa mevcut davranis testleri degistirilmeden gecmelidir.

## 9. Definition of Done

Bir uygulama task'i ancak su kosullarda tamamlanmis sayilir:

- Onaylanan Gherkin senaryolari karsilanmistir.
- Ilgili xUnit testleri gecmektedir.
- Tam test paketi gecmektedir.
- `dotnet build OpsDesk.sln` uyarisiz ve hatasiz tamamlanmistir.
- Migration varsa temiz PostgreSQL uzerinde dogrulanmistir.
- Swagger ve README degisen API davranisiyla uyumludur.
- Secret veya ilgisiz dosya degisikligi yoktur.
- Kullanici sonucu inceleyip kabul etmistir.

Testi gecirmek icin testi silmek, assertion'i zayiflatmak, testi skip etmek veya gercek hatayi gizlemek yasaktir.
