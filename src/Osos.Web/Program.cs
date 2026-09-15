using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Osos.Shared.Services;
using Osos.Web;
using Osos.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Backend API adresi. Sunucu WASM'i kendisi barındırdığında aynı origin kullanılır (CORS/mixed-content yok).
// Web'i ayrı barındırırsanız wwwroot/appsettings.json → ApiBaseUrl ile farklı adres verebilirsiniz.
var apiBase = builder.Configuration["ApiBaseUrl"];
if (string.IsNullOrWhiteSpace(apiBase)) apiBase = builder.HostEnvironment.BaseAddress;

// WASM'de token'ı localStorage'da sakla (AddOsosShared'dan önce → TryAdd onu kullanır).
builder.Services.TryAddSingleton<ITokenStore, LocalStorageTokenStore>();
builder.Services.AddOsosShared(apiBase);

var host = builder.Build();

// Saklı token'ı yükle
var api = host.Services.GetRequiredService<OsosApiClient>();
await api.InitializeAsync();

await host.RunAsync();
