using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Osos.Maui.Services;
using Osos.Shared.Services;

namespace Osos.Maui;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
			});

		builder.Services.AddMauiBlazorWebView();

		// Backend API adresi:
		//  - Android emülatör: 10.0.2.2 host makineye işaret eder
		//  - Windows masaüstü: localhost
		string apiBase =
#if ANDROID
			"http://10.0.2.2:5199/";
#else
			"http://localhost:5199/";
#endif

		builder.Services.TryAddSingleton<ITokenStore, SecureStorageTokenStore>();
		builder.Services.AddOsosShared(apiBase);

#if DEBUG
		builder.Services.AddBlazorWebViewDeveloperTools();
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
