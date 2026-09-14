# OSOS Suite (.NET)

UEDAŞ OSOS portalının (`osos.uedas.com.tr`) API'lerini kullanan, **Web + Android + Windows masaüstü + tablet + mobil** çalışan çok platformlu .NET uygulaması. Yapılan aramalar (parametre + sonuç) **MSSQL**'e kaydedilir, çok kullanıcı destekli.

## Mimari

```
OsosSuite.sln
├─ src/Osos.Core        OSOS entegrasyonu: CryptoJS-uyumlu AES + OsosClient + modeller
├─ src/Osos.Contracts   İstemci/sunucu ortak DTO'ları
├─ src/Osos.Server      ASP.NET Core Web API + EF Core + MSSQL (OSOS proxy + arama geçmişi)
├─ src/Osos.Shared      Razol Class Library: paylaşılan Blazor UI (tüm platformlar)
├─ src/Osos.Web         Blazor WebAssembly (tarayıcı)
├─ src/Osos.Maui        MAUI Blazor Hybrid (Android + Windows)
├─ src/Osos.Cli         OSOS canlı doğrulama aracı (login + servis)
└─ tests/Osos.Core.Tests  Kripto uyum testleri (gerçek CryptoJS vektörü)
```

Tüm istemciler yalnızca **Osos.Server**'ı çağırır. Sunucu OSOS ile şifreli konuşur
(AES: `salt+iv+base64`, `PBKDF2(passPhrase, salt, 128bit, 1 tur, SHA1)`, AES-CBC/PKCS7),
sonuçları MSSQL'e kaydeder. Bu tasarım tarayıcının cross-origin sorununu çözer ve
mobilde DB kimlik bilgisi taşınmasını önler.

## Kurulum & çalıştırma

Gereksinimler: .NET 10 SDK, MSSQL (LocalDB yeterli), MAUI workload (android/windows).

### 1) Veritabanı
`src/Osos.Server/appsettings.json` içindeki `ConnectionStrings:Default` MSSQL adresinizi gösterir
(varsayılan: LocalDB). Migration'lar sunucu açılışında otomatik uygulanır. Elle:
```bash
dotnet ef database update --project src/Osos.Server
```

### 2) Backend
```bash
dotnet run --project src/Osos.Server --urls http://localhost:5199
```
Swagger: `http://localhost:5199/swagger` (Development). 

### 3) Web (tarayıcı)
```bash
dotnet run --project src/Osos.Web --urls http://localhost:5100
```
`http://localhost:5100` → Kayıt ol → OSOS hesabını bağla → Sorgu / Geçmiş.

### 4) Masaüstü / mobil (MAUI)
```bash
# Windows masaüstü
dotnet build src/Osos.Maui -f net10.0-windows10.0.19041.0
# Android (emülatör/cihaz) — backend'e 10.0.2.2:5199 üzerinden bağlanır
dotnet build src/Osos.Maui -f net10.0-android
```

## OSOS bağlantısını doğrulama (CLI)

reCAPTCHA sunucuda zorunlu değilse programatik login çalışır. Kendi OSOS bilgilerinizle:
```bash
dotnet run --project src/Osos.Cli -- <OSOS_KullaniciAdi>
# Şifre gizli olarak sorulur; yalnızca yerelde kullanılır.
```
`✓ Login başarılı` görürseniz uygulamadan **Hesap → OSOS hesabı bağla** ile aynı bilgileri girin.
`✗ SessionKey alınamadı` çıkarsa captcha zorunludur → WebView login fallback gerekir (kod hazır, `plan` notlarına bakın).

## Özellikler / ekranlar
- **Tüketim, Endeks, Akım/Gerilim/Cosφ, Aboneler, Dashboard** sorguları.
- Her sorgu MSSQL'e kaydedilir (parametre + sonuç anlık görüntüsü).
- **Geçmiş**: listeleme, sonucu görüntüleme, tek tıkla tekrar çalıştırma, silme.
- Çok kullanıcı: ASP.NET Identity + JWT.

## Doğrulama durumu
- ✅ Kripto CryptoJS ile **byte-birebir** (gerçek vektörle 4/4 test).
- ✅ Backend uçtan uca (register/login/searches/osos-guard) — MSSQL LocalDB'de çalıştı.
- ✅ Web WASM: kayıt → giriş → UI, backend ile canlı doğrulandı.
- ✅ MAUI Android + Windows hedefleri derleniyor (paylaşılan UI).
- ⏳ OSOS canlı login: kullanıcının kendi kimlik bilgisiyle CLI/uygulamadan doğrulanacak.

## Güvenlik notları
- OSOS şifresi DB'de ASP.NET **DataProtection** ile şifreli saklanır.
- Üretimde `appsettings.json`'daki `Jwt:Key` mutlaka değiştirilmeli.
