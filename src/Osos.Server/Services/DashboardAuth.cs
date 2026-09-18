using System.Text;
using Hangfire.Dashboard;

namespace Osos.Server.Services;

/// <summary>
/// Hangfire dashboard'una HTTP Basic auth. Kullanıcı/şifre config'ten gelir.
/// Şifre boşsa (yapılandırılmamışsa) erişim tamamen reddedilir (public'e karşı güvenli varsayılan).
/// </summary>
public sealed class BasicAuthDashboardFilter : IDashboardAuthorizationFilter
{
    private readonly string _user;
    private readonly string _password;

    public BasicAuthDashboardFilter(string user, string password)
    {
        _user = user;
        _password = password;
    }

    public bool Authorize(DashboardContext context)
    {
        var http = context.GetHttpContext();

        if (string.IsNullOrEmpty(_password))
            return false; // yapılandırılmadıysa kimseye açma

        string? header = http.Request.Headers.Authorization;
        if (header is not null && header.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var creds = Encoding.UTF8.GetString(Convert.FromBase64String(header["Basic ".Length..].Trim()));
                int i = creds.IndexOf(':');
                if (i > 0 && creds[..i] == _user && creds[(i + 1)..] == _password)
                    return true;
            }
            catch { /* geçersiz header */ }
        }

        // Tarayıcıya parola sor
        http.Response.StatusCode = StatusCodes.Status401Unauthorized;
        http.Response.Headers.WWWAuthenticate = "Basic realm=\"Hangfire\"";
        return false;
    }
}
