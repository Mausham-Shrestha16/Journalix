using Courseworksample.Data;
using Courseworksample.Services;


namespace Courseworksample;

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

       

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
#endif

        // SQLite + Repo (slides-style usage through DI)
        builder.Services.AddSingleton<AppDatabase>();
        builder.Services.AddSingleton<JournalRepository>();

        // Security (PIN)
        builder.Services.AddSingleton<SecurityService>();

        // App lock state (simple in-memory)
        // App lock state (simple in-memory)
        builder.Services.AddSingleton<AppLockState>();

        // Theme service for light/dark mode
        builder.Services.AddSingleton<ThemeService>();

        // Authentication service
        builder.Services.AddSingleton<AuthService>();

        return builder.Build();
    }
}

// Simple lock state shared across pages
public class AppLockState
{
    public bool IsUnlocked { get; set; } = false;
}
