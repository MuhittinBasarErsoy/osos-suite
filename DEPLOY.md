# Ubuntu Sunucuya Deploy (Docker + Tailscale)

Uygulama tek container (web+API) + SQL Server container olarak çalışır. Dışarıya **hiçbir port açılmaz**; erişim **Tailscale** üzerinden HTTPS ile sağlanır.

## 0. Ön koşullar (sunucuda)
- Ubuntu 22.04+ , Tailscale kurulu ve bağlı (`tailscale status` çalışıyor)
- Sudo yetkisi

## 1. Docker kurulumu (bir kez)
```bash
curl -fsSL https://get.docker.com | sudo sh
sudo usermod -aG docker $USER        # sonra yeniden giriş yapın (logout/login)
docker version && docker compose version
```

## 2. Projeyi çek
Repo private olduğu için GitHub kimlik doğrulaması gerekir (Personal Access Token ile en kolay):
```bash
cd ~
git clone https://github.com/MuhittinBasarErsoy/osos-suite.git
# Kullanıcı adı: MuhittinBasarErsoy | Şifre: GitHub PAT (classic, repo yetkili)
cd osos-suite
```
> Güncelleme geldiğinde: `git pull` sonra 4. adımı tekrar çalıştırın.

## 3. Gizli değerler (.env)
```bash
cp .env.example .env
nano .env    # MSSQL_SA_PASSWORD ve JWT_KEY'i güçlü değerlerle doldurun
# JWT_KEY üretmek için: openssl rand -base64 48
```

## 4. Başlat
```bash
docker compose -f docker-compose.yml -f docker-compose.prod.yml up -d --build
```
İlk build birkaç dakika (imaj indirme + publish). Sonra:
```bash
docker compose logs -f app          # "Now listening on: http://[::]:8080" görün
curl -s -o /dev/null -w "%{http_code}\n" http://127.0.0.1:8080   # 200 beklenir
```

## 5. Tailscale ile yayınla (HTTPS, sadece tailnet)
```bash
# Tailnet admin panelinde HTTPS sertifikaları + MagicDNS açık olmalı (bir kez).
sudo tailscale serve --bg 8080
tailscale serve status
```
Artık uygulamaya **tailnet'teki her cihazdan** şu adresle erişilir:
```
https://<sunucu-adı>.<tailnet-adı>.ts.net
```
(`tailscale status` ilk sütun = sunucu adı. HTTPS'i Tailscale otomatik sağlar; ekstra sertifika/port yok.)

Kapatmak için: `sudo tailscale serve --https=443 off`

## 6. Veritabanına SSMS ile bağlanma (opsiyonel, tailnet üzerinden)
DB portu public değil. İki yol:

**A) SSH tüneli (basit):** Windows'tan
```bash
ssh -L 1433:127.0.0.1:1433 <kullanıcı>@<sunucu-tailscale-ip>
```
Sonra SSMS: `localhost,1433`, `sa`, `.env`'deki şifre, "Sunucu Sertifikasına Güven".

**B) Tailscale TCP serve:** sunucuda
```bash
sudo tailscale serve --bg --tcp=1433 tcp://127.0.0.1:1433
```
Sonra SSMS: `<sunucu-adı>.<tailnet>.ts.net,1433`.

## Yönetim komutları
```bash
docker compose -f docker-compose.yml -f docker-compose.prod.yml ps        # durum
docker compose -f docker-compose.yml -f docker-compose.prod.yml logs -f   # loglar
docker compose -f docker-compose.yml -f docker-compose.prod.yml restart app
docker compose -f docker-compose.yml -f docker-compose.prod.yml down       # durdur (veri kalır)
```

## Notlar
- Veriler `mssql-data` adlı Docker volume'unda **kalıcı**; `down` ver/silmez (`down -v` siler).
- Zamanlanmış Job'ların (Hangfire) çalışması için container **açık** kalmalı (`restart: always` ile sunucu yeniden başlasa da otomatik kalkar).
- Hangfire paneli `/<...>ts.net/hangfire` — şu an herkese açık; istenirse kısıtlanır.
- OSOS'a giden istekler sunucudan çıkar (internet erişimi gerekir); OSOS IP kısıtlaması varsa sunucu IP'sinin izinli olması gerekebilir.
- MAUI mobil uygulama için API adresi bu tailnet HTTPS adresi olur (mobil cihaz da tailnet'te olmalı).
