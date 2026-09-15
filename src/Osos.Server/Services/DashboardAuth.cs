using Hangfire.Dashboard;

namespace Osos.Server.Services;

/// <summary>Geliştirme için Hangfire dashboard'una herkese izin verir. ÜRETİMDE kısıtlayın.</summary>
public sealed class AllowAllDashboardAuth : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context) => true;
}
