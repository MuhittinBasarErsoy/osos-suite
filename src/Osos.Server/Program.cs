using System.Text;
using Hangfire;
using Hangfire.SqlServer;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Osos.Server.Data;
using Osos.Server.Services;

var builder = WebApplication.CreateBuilder(args);

// ---- Konfigürasyon ----
var jwt = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
if (string.IsNullOrWhiteSpace(jwt.Key))
    jwt.Key = "CHANGE_ME_dev_only_super_secret_key_change_in_production_1234567890";
builder.Services.AddSingleton(jwt);

var connStr = builder.Configuration.GetConnectionString("Default")
    ?? "Server=(localdb)\\MSSQLLocalDB;Database=OsosSuite;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=true;Connect Timeout=60";

// ---- EF Core + Identity ----
// LocalDB boştayken otomatik durur; ilk istekte soğuk başlatma gecikir → yeniden deneme + uzun timeout.
builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlServer(connStr, sql =>
    sql.EnableRetryOnFailure(maxRetryCount: 6, maxRetryDelay: TimeSpan.FromSeconds(10), errorNumbersToAdd: null)));
builder.Services.AddIdentityCore<AppUser>(o =>
    {
        o.Password.RequiredLength = 6;
        o.Password.RequireNonAlphanumeric = false;
        o.User.RequireUniqueEmail = false;
    })
    .AddEntityFrameworkStores<AppDbContext>();

// ---- Auth (JWT) ----
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key))
        };
    });
builder.Services.AddAuthorization();

// ---- Uygulama servisleri ----
builder.Services.AddDataProtection();
builder.Services.AddSingleton<OsosSessionService>();
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<ResultMaterializer>();
builder.Services.AddScoped<SearchService>();

// ---- Hangfire (zamanlanmış/anlık işler) ----
builder.Services.AddHangfire(cfg => cfg
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSqlServerStorage(connStr, new SqlServerStorageOptions
    {
        PrepareSchemaIfNecessary = true,
        QueuePollInterval = TimeSpan.FromSeconds(15)
    }));
builder.Services.AddHangfireServer();
builder.Services.AddScoped<JobRunner>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ---- CORS (web istemcisi için) ----
// Kimlik bearer token (Authorization header) ile taşınır, cookie kullanılmaz →
// AllowCredentials gerekmez, bu yüzden geliştirmede her origin'e izin veriyoruz (port/https esnekliği).
const string CorsPolicy = "clients";
builder.Services.AddCors(o => o.AddPolicy(CorsPolicy, p =>
    p.SetIsOriginAllowed(_ => true).AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

// ---- DB migrate (geliştirme kolaylığı) ----
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (db.Database.IsRelational())
    {
        try { db.Database.Migrate(); }
        catch (Exception ex) { app.Logger.LogWarning(ex, "DB migrate atlandı (MSSQL erişilemiyor olabilir)."); }
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Blazor WASM istemcisini aynı sunucudan sun (tek uygulama, tek origin).
app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

app.UseHttpsRedirection();
app.UseCors(CorsPolicy);
app.UseAuthentication();
app.UseAuthorization();

// Hangfire dashboard (dev: herkese açık — üretimde kısıtlayın)
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new AllowAllDashboardAuth() }
});

app.MapControllers();

// API dışındaki tüm yollar Blazor index.html'e düşer (SPA yönlendirmesi).
app.MapFallbackToFile("index.html");

app.Run();
