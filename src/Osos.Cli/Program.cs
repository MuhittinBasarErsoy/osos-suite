using System.Net;
using System.Text.Json;
using Osos.Core.Osos;

// OSOS canlı doğrulama aracı.
// Kullanım:
//   dotnet run --project src/Osos.Cli -- <UserCode> <Password> [Serno]
// Şifre parametre olarak verilmezse gizli şekilde sorulur. Şifre yalnızca yerelde kullanılır.

string? userCode = args.Length > 0 ? args[0] : Prompt("OSOS Kullanıcı Adı: ");
string? password = args.Length > 1 ? args[1] : PromptSecret("OSOS Şifre: ");
long serno = args.Length > 2 && long.TryParse(args[2], out var s) ? s : 0;

if (string.IsNullOrWhiteSpace(userCode) || string.IsNullOrWhiteSpace(password))
{
    Console.WriteLine("Kullanıcı adı ve şifre gerekli.");
    return 1;
}

var cookies = new CookieContainer();
using var handler = new HttpClientHandler { UseCookies = true, CookieContainer = cookies, AllowAutoRedirect = true };
using var http = new HttpClient(handler) { BaseAddress = new Uri(OsosClient.DefaultBaseUrl) };
http.DefaultRequestHeaders.Add("Accept", "application/json");

var client = new OsosClient(http);

Console.WriteLine("→ Login deneniyor (captcha token olmadan)...");
LoginResponse login;
try
{
    login = await client.LoginAsync(userCode!, password!);
}
catch (Exception ex)
{
    Console.WriteLine($"✗ Login isteği hata verdi: {ex.Message}");
    return 2;
}

if (string.IsNullOrWhiteSpace(login.SessionKey))
{
    Console.WriteLine($"✗ SessionKey alınamadı. IsSuccess={login.IsSuccess}, Message={login.Message}, ResultCode={login.ResultCode}");
    Console.WriteLine("  (Captcha sunucuda zorunlu olabilir → WebView login fallback gerekir.)");
    return 3;
}

Console.WriteLine($"✓ Login başarılı. SessionKey uzunluğu: {login.SessionKey!.Length}");

// Abone listesi (küçük, hızlı doğrulama)
Console.WriteLine("→ GetCustomerPortalSubscriptions çağrılıyor...");
try
{
    using var subs = await client.CallJsonAsync(login.SessionKey!, OsosMethods.GetCustomerPortalSubscriptions,
        new { Serno = serno, PageSize = 10, PageNumber = 1 });
    string preview = subs.RootElement.GetRawText();
    Console.WriteLine("✓ Yanıt alındı (ilk 500 karakter):");
    Console.WriteLine(preview.Length > 500 ? preview[..500] + "..." : preview);
}
catch (Exception ex)
{
    Console.WriteLine($"✗ Servis çağrısı hata: {ex.Message}");
    return 4;
}

Console.WriteLine("\n✓ Uçtan uca doğrulama tamam. Backend entegrasyonu bu akışı kullanabilir.");
return 0;

static string? Prompt(string label)
{
    Console.Write(label);
    return Console.ReadLine();
}

static string? PromptSecret(string label)
{
    Console.Write(label);
    var sb = new System.Text.StringBuilder();
    ConsoleKeyInfo key;
    while ((key = Console.ReadKey(intercept: true)).Key != ConsoleKey.Enter)
    {
        if (key.Key == ConsoleKey.Backspace) { if (sb.Length > 0) sb.Length--; }
        else if (!char.IsControl(key.KeyChar)) sb.Append(key.KeyChar);
    }
    Console.WriteLine();
    return sb.ToString();
}
