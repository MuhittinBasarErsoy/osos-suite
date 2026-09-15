# OSOS Suite — Durum / Devir Dokümanı

> Bu dosya, projeye başka bir geliştirici/AI aracının kaldığı yerden devam edebilmesi için hazırlanmıştır.
> Son güncelleme: 2026-09-15

## 1. Amaç

UEDAŞ OSOS portalının (`https://osos.uedas.com.tr`) API'lerini kullanan; **Web, Android, Windows masaüstü, tablet, mobil** platformlarında çalışan .NET uygulaması. Kullanıcının portalda yaptığı sorguların **parametre + sonuçlarını MSSQL'e** kaydeder, tekrar çalıştırır, CSV'ye aktarır. Çok kullanıcı destekli.

- **Repo (private):** https://github.com/MuhittinBasarErsoy/osos-suite
- **.NET sürümü:** net10.0 (SDK 10.0.x kurulu). MAUI workload: android + windows (iOS/Mac v1 dışı).

## 2. Mimari

```
OsosSuite.slnx
├─ src/Osos.Core        OSOS entegrasyonu: CryptoJS-uyumlu AES + OsosClient + modeller
├─ src/Osos.Contracts   İstemci/sunucu ortak DTO'ları
├─ src/Osos.Server      ASP.NET Core Web API + EF Core + MSSQL + Identity/JWT (OSOS proxy + geçmiş)
├─ src/Osos.Shared      Razor Class Library: paylaşılan Blazor UI (tüm platformlar tek kod tabanı)
├─ src/Osos.Web         Blazor WebAssembly (tarayıcı)
├─ src/Osos.Maui        MAUI Blazor Hybrid (Android + Windows)
├─ src/Osos.Cli         OSOS canlı doğrulama aracı (login + servis testi)
└─ tests/Osos.Core.Tests  Kripto uyum testleri
```

**İlke:** Tüm istemciler yalnızca `Osos.Server`'ı çağırır. Sunucu OSOS ile şifreli konuşur, sonuçları MSSQL'e kaydeder. (Tarayıcı cross-origin OSOS'a gidemez; mobilde DB şifresi taşınmaz — bu yüzden merkezi backend.)

## 3. OSOS entegrasyonu — KRİTİK teknik detaylar

OSOS bir Angular SPA + `/aril-portalserver/api` altında **ESB-gateway (RPC)**. `customer-esb`'e `{MethodName, Parameters, IsAsync, Application:"PORTAL"}` POST edilir. **İstek ve yanıt gövdeleri istemci tarafı AES ile şifrelidir.**

### 3.1 Şifreleme (CryptoJS uyumlu) — `src/Osos.Core/Crypto/OsosCrypto.cs`
- Format: `saltHex(32) + ivHex(32) + Base64(ciphertext)`
- Anahtar: `PBKDF2(passPhrase, saltBytes, iterations=1000, HMAC-SHA1, 16 byte)` → **iterations = 1000** (bundle'da `iterationCount=1e3`; DİKKAT: yanlışlıkla 1 alınırsa her şey "Hatalı istek" verir)
- AES-128-CBC / PKCS7, verilen IV ile
- **passPhrase:**
  - `login` / `nologin` / `forgotPassword` uçları → sabit `"nQ16mjwHIrpH9IVobutgTTms8TuibkFagoWMNWguRckcqxQ"`
  - Diğer uçlar (`customer-esb`) → login yanıtındaki `SessionKey`
- Doğrulama: `tests/Osos.Core.Tests` gerçek CryptoJS test vektörüyle key + ciphertext byte-birebir eşleşiyor (4/4 geçiyor).

### 3.2 Login — `src/Osos.Core/Osos/OsosClient.cs`
Gerçek login gövdesi (tarayıcıdan yakalandı) SADECE şu alanlar:
```json
{"UserCode":"...","Password":"...","LoginType":1,"RememberMe":false,"LoginPhase":1}
```
- **reCAPTCHA sunucuda ZORUNLU DEĞİL** — `GRecaptchaResponse` alanı boşken gönderilmez (fazladan gönderilirse "Hatalı istek" olur). Captcha'sız login çalışıyor.
- **HTTP başlıkları şart:** İstemci `User-Agent` (+ Origin/Referer) göndermezse OSOS **1006** ("İstek sırasında hata") döner. OsosClient ctor'da tarayıcı benzeri başlıklar ekleniyor. (Bu, en zor bulunan tuzaktı.)
- Login öncesi `checklogin` GET ile TS (F5 BIG-IP) cookie'si alınıyor (`HttpClientHandler{UseCookies=true}`).
- Hata kodları: `10015` = kullanıcı adı/parola hatalı, `1006` = eksik başlık/yapısal, `11` = bozuk istek.

### 3.3 Login yanıtı (önemli alanlar)
`SessionKey`, `Serno` (müşteri Serno'su, ör. 675625 — sorgularda kullanılır), `IdentifierValue` (e-posta), `Subscriptions` (tesisat/abone listesi, her biri kendi `Serno`'su ile), `IsTwoFactorAutEnable`.
- **Müşteri Serno otomatik:** Link sırasında yakalanıp saklanıyor; sorgularda Serno boşsa backend otomatik dolduruyor (`OsosController.ResolveSernoAsync`). Kullanıcı Serno girmez.

### 3.4 Tespit edilen MethodName'ler (canlı + statik)
`GetCustomerPortalSubscriptions`, `GetCustomerMarks`, `GetCustomerSelectedCurrentEndexes`, `GetCustomerSelectedConsumptions`, `GetCustomerSelectedProfiles` (dikkat: alan adı `WithourMultiplier` — OSOS yazım hatası korunmalı), `GetOwnerConsumptions`, `GetOwnerMontlyEndexConsumptions`, `GetDefinitionBySernoParam`, `ExecuteDynamicReport`, `LoadDashboardWidgetContent`, `GetActivePortalAnnouncements`.
- Tarih formatı: `yyyyMMddHHmmss` (long).
- Detaylı API haritası: `OSOS_API_Haritasi.md`.

## 4. Backend (Osos.Server)

- **DB:** MSSQL LocalDB (`(localdb)\MSSQLLocalDB`, veritabanı `OsosSuite`). Connection string `appsettings.json` → `ConnectionStrings:Default`. EF Core migration'lar açılışta otomatik uygulanıyor. Bağlantıya `Connect Timeout=60` + `EnableRetryOnFailure` (LocalDB soğuk başlatma toleransı).
- **Kimlik:** ASP.NET Identity + JWT. `Jwt:Key` şu an **geliştirme placeholder**'ı — üretimde değiştirilmeli.
- **OSOS şifresi:** DB'de DataProtection ile şifreli (`OsosCredential.OsosPasswordProtected`).
- **CORS:** dev'de her origin'e açık (bearer token ile, cookie yok).
- **Tablolar:** AspNet* (Identity), `OsosCredentials` (+`CustomerSerno`), `SearchHistories`, `SearchResultSnapshots`.

### REST uçları
- `POST /api/auth/register`, `POST /api/auth/login` → JWT
- `POST /api/osos/link` → OSOS hesabı bağla/doğrula (Serno'yu saklar)
- `GET  /api/osos/me` → müşteri Serno + tesisat listesi
- `POST /api/osos/consumption | endex | profiles | subscriptions | dashboard/owner-consumptions` → sorgu + otomatik MSSQL kayıt
- `GET  /api/searches` (sayfalı), `GET /api/searches/{id}`, `POST /api/searches/{id}/rerun`, `GET /api/searches/{id}/export` (CSV), `DELETE /api/searches/{id}`

## 5. Frontend (Shared UI)

- Sayfalar: `Home`, `Account` (giriş/kayıt + OSOS bağla), `Query` (Sorgu), `History` (Geçmiş).
- `Query`: Ekran seçici + tarih aralığı; Serno GİRİLMEZ (otomatik). Tüketim/Endeks/Profil'de tesisat filtresi (çoklu seçim). Dashboard'da tesisat dropdown. Sonuçta **Tablo/Grafik** sekmesi + **CSV indir**.
- `ResultTable`: OSOS JSON'unu tabloya çevirir. `ResultChart`: inline SVG bar grafik (harici kütüphane yok, WASM/offline uyumlu).
- Dosya indirme: `wwwroot/osos.js` → `window.ososDownload`. Web + MAUI `index.html`'e script eklendi.
- Token saklama: Web → localStorage (`LocalStorageTokenStore`), MAUI → SecureStorage (`SecureStorageTokenStore`).
- MAUI API adresi: Android emülatör `http://10.0.2.2:5199`, Windows `http://localhost:5199` (`MauiProgram.cs`).

## 6. Nasıl çalıştırılır  (TEK UYGULAMA — güncellendi)

Osos.Server artık Blazor WASM web'i de kendisi barındırıyor (hosted model). Yani **tek proje çalışır**, web + API aynı adreste. CORS / mixed-content / iki-proje koordinasyonu YOK.

- **Visual Studio:** Startup project = **yalnızca Osos.Server** (Multiple startup KULLANMAYIN). F5 → tarayıcı `https://localhost:7085` açılır (hem web hem API).
- **Terminal:**
  ```bash
  dotnet run --project src/Osos.Server --urls https://localhost:7085
  ```
  Sonra tarayıcı: `https://localhost:7085`

Akış: Kayıt ol → **Hesap → OSOS hesabı bağla** (gerçek OSOS kullanıcı adı/şifre) → Sorgu / Geçmiş.

**Notlar:**
- Web ile API aynı origin olduğu için `apiBase` boş (aynı origin). Web'i AYRI barındırırsanız `src/Osos.Web/wwwroot/appsettings.json` → `ApiBaseUrl` verin.
- **Derleme kilidi:** Uygulama çalışırken Rebuild yaparsanız "file locked by Osos.Server" çıkar → önce durdurun (VS Stop / Ctrl+C).
- LocalDB boşta durursa ilk istekte retry devreye girer; takılırsa `sqllocaldb start MSSQLLocalDB`.
- Dev HTTPS sertifikası güvenilir olmalı (VS kurulumu genelde halleder; gerekirse `dotnet dev-certs https --trust`).

**Not (VS debugger):** "fatal error loading metadata for System.Private.CoreLib" çıkarsa `.vs` klasörünü silip Rebuild yapın, ya da Ctrl+F5 / terminalden çalıştırın.

## 7. Doğrulama durumu

- ✅ Kripto CryptoJS ile byte-birebir (4/4 test)
- ✅ OSOS login + sorgular canlı çalışıyor (gerçek hesapla; DB'de gerçek snapshot'lar var)
- ✅ Backend (register/login/searches/link/export) uçtan uca test edildi
- ✅ CSV export test edildi (BOM + doğru içerik)
- ✅ Web WASM canlı; MAUI Android + Windows derleniyor
- ⏳ Kullanıcı testi bekleyen: grafik doğruluğu, tesisat filtresinin gerçekten filtrelemesi, abone etiketleri
- ⏳ MAUI gerçek cihazda çalıştırılmadı (sadece derlendi)

## 8. Kalan işler (öncelik sırası)

1. **Deployment** (en kritik): backend'i bir sunucuya host et (HTTPS + gerçek `Jwt:Key` + prod MSSQL); istemciler sabit backend adresi kullansın. Bu olmadan uygulama sadece bu bilgisayarda çalışır.
2. **MAUI'yi gerçek cihazda çalıştırma/test** (Android emülatör/telefon, Windows).
3. **Cila:** gerçek `.xlsx` (şu an CSV), yükleniyor animasyonları, oturum yenileme UX, çizgi grafik + tarih ekseni.
4. **Tesisat filtresi doğrulama:** OSOS `Selected` alanının tesisat serno'larıyla gerçekten filtrelediğini canlı teyit; gerekirse ince ayar.

## 9. Faydalı komutlar

```bash
# Testler
dotnet test tests/Osos.Core.Tests
# Migration
dotnet ef migrations add <Ad> --project src/Osos.Server
dotnet ef database update --project src/Osos.Server
# CLI ile OSOS login doğrulama (şifre gizli sorulur)
dotnet run --project src/Osos.Cli -- <OSOS_KullaniciAdi>
```

## 10. Güvenlik notları

- Repo private. `.gitignore` bin/obj + secrets.json + appsettings.*.local.json hariç tutuyor.
- Üretim öncesi: `Jwt:Key` değiştir, prod MSSQL bağlantısı user-secrets/ortam değişkeni ile ver, HTTPS kullan.
- Not: Geliştirme sırasında OSOS şifresi düz metin görüldü (yapı analizi için); güvenlik açısından şifre değişikliği önerilir. Şifre repoda saklanmıyor.
