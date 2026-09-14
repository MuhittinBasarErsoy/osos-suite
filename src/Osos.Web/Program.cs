using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Osos.Shared.Services;
using Osos.Web;
using Osos.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Backend API adresi (geliştirme). Yayında appsettings/ortam ile değiştirilebilir.
var apiBase = builder.Configuration["ApiBaseUrl"] ?? "http://localhost:5199/";

// WASM'de token'ı localStorage'da sakla (AddOsosShared'dan önce → TryAdd onu kullanır).
builder.Services.TryAddSingleton<ITokenStore, LocalStorageTokenStore>();
builder.Services.AddOsosShared(apiBase);

var host = builder.Build();

// Saklı token'ı yükle
var api = host.Services.GetRequiredService<OsosApiClient>();
await api.InitializeAsync();

await host.RunAsync();
