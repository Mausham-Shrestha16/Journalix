using Microsoft.Maui.Storage;

namespace Courseworksample.Services;

/// <summary>
/// Service for managing application theme (Light/Dark mode)
/// Persists theme preference using MAUI Preferences API
/// </summary>
public class ThemeService
{
    private const string THEME_KEY = "app_theme";
    private const string LIGHT_THEME = "light";
    private const string DARK_THEME = "dark";

    /// <summary>
    /// Event triggered when theme changes
    /// </summary>
    public event Action? OnThemeChanged;

    /// <summary>
    /// Gets the current theme
    /// </summary>
    public string CurrentTheme { get; private set; }

    /// <summary>
    /// Checks if dark mode is enabled
    /// </summary>
    public bool IsDarkMode => CurrentTheme == DARK_THEME;

    public ThemeService()
    {
        // Load saved theme preference or default to light
        CurrentTheme = Preferences.Get(THEME_KEY, LIGHT_THEME);
    }

    /// <summary>
    /// Toggles between light and dark theme
    /// </summary>
    public void ToggleTheme()
    {
        CurrentTheme = IsDarkMode ? LIGHT_THEME : DARK_THEME;
        Preferences.Set(THEME_KEY, CurrentTheme);
        OnThemeChanged?.Invoke();
    }

    /// <summary>
    /// Sets a specific theme
    /// </summary>
    /// <param name="isDark">True for dark mode, false for light mode</param>
    public void SetTheme(bool isDark)
    {
        var newTheme = isDark ? DARK_THEME : LIGHT_THEME;
        if (CurrentTheme != newTheme)
        {
            CurrentTheme = newTheme;
            Preferences.Set(THEME_KEY, CurrentTheme);
            OnThemeChanged?.Invoke();
        }
    }

    /// <summary>
    /// Gets the CSS class name for current theme
    /// </summary>
    public string GetThemeClass()
    {
        return IsDarkMode ? "theme-dark" : "theme-light";
    }
}