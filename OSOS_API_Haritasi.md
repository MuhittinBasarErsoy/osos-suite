# OSOS Portal API Haritası — osos.uedas.com.tr

> Angular SPA. Backend base URL: `/aril-portalserver/api`
> Mimari: **ESB-gateway (RPC)** — veri işlemleri tek endpoint'e POST edilir, işlem gövdedeki `MethodName` ile seçilir.
> **İstek ve yanıt gövdeleri istemci tarafında AES ile şifrelenir** (aşağıda şema).

---

## 1. Transport / kimlik endpoint'leri

| Endpoint | Method | Açıklama |
|---|---|---|
| `/aril-portalserver/api/login` | POST | Giriş. Gövde: `UserCode, Password, LoginType, RememberMe, RememberToken, Token, LoginPhase` (alternatif: `SerialNumber, ExternalAreaCode`) |
| `/aril-portalserver/api/checklogin` | GET | Oturum geçerlilik kontrolü |
| `/aril-portalserver/api/logout` | GET | Çıkış |
| `/aril-portalserver/api/nologin` | POST | Login öncesi ESB çağrıları (const anahtarla şifreli) |
| `/aril-portalserver/api/forgotPassword` | POST | `UserCode, UserEmail, ResetPhase, LoginType` |
| `/aril-portalserver/api/validateEmailToken` | POST | `Token` |
| `/aril-portalserver/api/customer-esb` | POST | **Müşteri veri gateway'i (ana API)** |
| `/aril-portalserver/api/supplier-esb` | POST | Tedarikçi veri gateway'i |
| `/aril-portalserver/api/export` | POST | Rapor/tablo dışa aktarımı (Excel/CSV/mail) |
| `/aril-portalserver/api/upload/profilephotos` | POST | Profil fotoğrafı yükleme |
| `/aril-portalserver/api/upload/signUp` | POST | Başvuru belge yükleme |
| `/assets/custom/config.json` | GET | İstemci config (`enableCustomerSignUp, captcha, kvkkUrl, termsUrl`) |

Kurumsal varyantlar (aynı kod, farklı `window.app`):
`/aril-corporateserver/api/forgotPassword`, `/aril-webserver/api/forgotPassword`, `/aril-webserver/api/upload`

---

## 2. ESB gateway istek zarfı (customer-esb / supplier-esb)

```json
{
  "MethodName": "<servis-adı>",
  "Parameters": { ... },        // obje veya doğrudan skaler (ör. Serno)
  "IsAsync": false,
  "Application": "PORTAL",
  "Headers": {},                // export/rapor çağrılarında
  "ExportOrders": [],           // export'ta kolon anahtarları
  "Pageable": true,             // export'ta
  "Type": "file",               // export: "file" | "mail"
  "FileType": "xls"             // export
}
```

Yanıt da aynı AES formatında döner; içinde tipik olarak `originalResponse` / veri kümesi bulunur.

---

## 3. MethodName (servis) listesi

### Canlı yakalanan (giriş sonrası, düz metin parametrelerle doğrulandı)

| MethodName | Ekran | Parametreler |
|---|---|---|
| `GetCustomerPortalSubscriptions` | Abone listesi | `{Serno, PageSize, PageNumber}` |
| `GetCustomerMarks` | Etiketler | `Serno` (skaler) |
| `GetCustomerSelectedCurrentEndexes` | Endeks (güncel) | `{Serno, StartDate, EndDate, Selected[], MarkFilterString, TitleFilterString, TotalItemCount}` |
| `GetCustomerSelectedConsumptions` | Tüketim | `{Serno, StartDate, EndDate, Selected[], Type, Period, MarkFilterString, TitleFilterString, TotalItemCount}` |
| `GetCustomerSelectedProfiles` | Akım/Gerilim/Cosφ (CVC) | `{Serno, StartDate, EndDate, Selected[], ..., WithourMultiplier}` |
| `GetDefinitionBySernoParam` | Anasayfa dashboard | `{Serno, DisplayItems[], DefinitionType, IncludeGroupInfo}` |
| `GetOwnerMontlyEndexConsumptions` | Anasayfa | `{OwnerSerno, OwnerIdentifier, OwnerIdentifierSec, EndDate, WithoutMultiplied, EndexDirection}` |
| `GetOwnerConsumptions` | Anasayfa | `{OwnerSerno, OwnerType, StartDate, EndDate, IsOnlySuccess, IncludeLoadProfiles, IncludeVersions, WithoutMultiplier, MergeResult}` |

> Tarih formatı: `YYYYMMDDhhmmss` (sayı). `Serno` = seçili abone/tesisat kaydı; `OwnerSerno` = sayaç/tesis sahibi kaydı.

### Statik bundle'da bulunan (kod içinde sabit geçen)

| MethodName | İşlev |
|---|---|
| `ExecuteDynamicReport` | Dinamik rapor çalıştırma (`Serno, ReportType, Version`) |
| `CrpExecuteDynamicReport` | Kurumsal dinamik rapor |
| `LoadDashboardWidgetContent` | Dashboard widget verisi (`Serno, PageSize, PageNumber`) |
| `GetActivePortalAnnouncements` | Aktif duyurular |
| `GetGatewayLogHistory` | Gateway log geçmişi |
| `CustomerRegistrationApplication` | Yeni müşteri başvurusu |
| `CheckCustomerSignUpApplicationStatus` | Başvuru durumu (`Token`) |
| `SendValidateEmail` | Doğrulama e-postası |
| `SearchCrossDomain` | Çapraz alan arama |

> Not: `MethodName` dinamik de atanabiliyor (kodda `MethodName: t.service`). Yani gateway keyfi servis adları kabul ediyor; tam liste backend'de. Yukarıdakiler istemcide gözlemlenen kümedir.

---

## 4. Şifreleme şeması (CryptoJS AES)

Kütüphane: CryptoJS (sayfada global). Sınıf `encryptionKey="narBusiness"` (yerel storage için) ayrı; **API için passPhrase = oturum anahtarı**.

**Format:** `salt(32 hex) + iv(32 hex) + base64(ciphertext)`

```
salt = WordArray.random(16) -> hex        // 16 byte
iv   = WordArray.random(16) -> hex        // 16 byte
key  = PBKDF2(passPhrase, saltHex, { keySize: 128/32, iterations: 1 })   // 128-bit, 1 tur
body = salt + iv + AES.encrypt(plaintext, key, { iv }).ciphertext(base64)  // AES-CBC
```

**passPhrase kaynağı** (`getSessionKey`):
- Login öncesi / const API'ler: sabit `"nQ16mjwHIrpH9IVobutgTTms8TuibkFagoWMNWguRckcqxQ"`
- Login sonrası: oturum anahtarı localStorage'da (`prt_sK` civarı, istemci içi anahtarla saklı)

**Çözme** (`decrypt`): `salt=[0:32]`, `iv=[32:64]`, `ct=[64:]`, `key=PBKDF2(passPhrase, salt)`, `AES.decrypt(ct, key, {iv})`.

localStorage anahtarları: `prt_sK` (session key), `prt_sdb`, `prt_dType`, `prt_cI`, `mlbryU` (kullanıcı, `"mulberryFreamwork"` ile AES).

---

## 5. Statik varlıklar / bundle

- SPA giriş: `main.<hash>.js`, `runtime.<hash>.js`, `polyfills`, `styles`, `scripts`
- Lazy chunk'lar: `4..10.<hash>.js` (rapor/dashboard modülleri)

---
*Çıkarım yöntemi: JS bundle statik analizi + giriş yapılmış oturumda `CryptoJS.AES.encrypt` sarmalanarak canlı düz-metin yakalama.*
