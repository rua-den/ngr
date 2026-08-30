using System.Windows;
using System.Windows.Media;
using Ngr.Launcher.Core.Configuration;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace Ngr.Launcher.App.Services;

public interface IThemeService
{
    void Apply(ThemePreference theme);
}

public sealed class WpfThemeService : IThemeService
{
    private static readonly string[] PaletteKeys =
    [
        "ApplicationBackgroundBrush",
        "CardBackgroundFillColorDefaultBrush",
        "CardBackgroundFillColorSecondaryBrush",
        "ControlFillColorDefaultBrush",
        "ControlFillColorSecondaryBrush",
        "ControlFillColorTertiaryBrush",
        "ControlFillColorInputActiveBrush",
        "ControlStrokeColorDefaultBrush",
        "DividerStrokeColorDefaultBrush",
        "SurfaceStrokeColorDefaultBrush",
        "TextFillColorPrimaryBrush",
        "TextFillColorSecondaryBrush",
        "TextFillColorTertiaryBrush",
        "LayerFillColorDefaultBrush",
        "LayerFillColorAltBrush",
        "SolidBackgroundFillColorBaseBrush",
        "SolidBackgroundFillColorSecondaryBrush"
    ];

    private Window? _window;
    private ThemePreference _currentTheme = ThemePreference.System;
    private bool _watchingSystemTheme;

    public WpfThemeService()
    {
        ApplicationThemeManager.Changed += OnApplicationThemeChanged;
    }

    public void Attach(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        if (_window is not null && !ReferenceEquals(_window, window))
        {
            StopWatchingSystemTheme();
        }

        _window = window;
        if (_currentTheme == ThemePreference.System)
        {
            StartWatchingSystemTheme();
        }
    }

    public void Apply(ThemePreference theme)
    {
        if (!Enum.IsDefined(theme))
        {
            throw new ArgumentOutOfRangeException(nameof(theme), theme, "Theme preference is invalid.");
        }

        if (theme != ThemePreference.System)
        {
            StopWatchingSystemTheme();
        }

        _currentTheme = theme;

        switch (theme)
        {
            case ThemePreference.System:
                ApplicationThemeManager.ApplySystemTheme();
                StartWatchingSystemTheme();
                break;
            case ThemePreference.Light:
                ApplicationThemeManager.Apply(ApplicationTheme.Light, WindowBackdropType.None);
                break;
            case ThemePreference.Dark:
                ApplicationThemeManager.Apply(ApplicationTheme.Dark, WindowBackdropType.None);
                break;
        }

        ApplyLauncherPalette(ApplicationThemeManager.GetAppTheme());
    }

    private static void OnApplicationThemeChanged(ApplicationTheme theme, Color accent)
    {
        ApplyLauncherPalette(theme);
    }

    private static void ApplyLauncherPalette(ApplicationTheme theme)
    {
        if (Application.Current is null)
        {
            return;
        }

        if (theme == ApplicationTheme.HighContrast)
        {
            ClearLauncherPalette(Application.Current.Resources);
            return;
        }

        var dark = theme == ApplicationTheme.Dark;
        var resources = Application.Current.Resources;

        // WPF-UI's stock dark brushes are intentionally very translucent. On an
        // opaque desktop shell that makes the window, cards and inputs collapse
        // into one nearly-black plane. NGR keeps WPF-UI for theme/accent plumbing
        // but uses a separated Material-inspired surface scale for readability.
        SetBrush(resources, "ApplicationBackgroundBrush", dark ? "#181A1F" : "#F6F7FB");
        SetBrush(resources, "CardBackgroundFillColorDefaultBrush", dark ? "#24272E" : "#FFFFFF");
        SetBrush(resources, "CardBackgroundFillColorSecondaryBrush", dark ? "#202329" : "#FAFBFC");
        SetBrush(resources, "ControlFillColorDefaultBrush", dark ? "#2C3038" : "#F1F3F7");
        SetBrush(resources, "ControlFillColorSecondaryBrush", dark ? "#343943" : "#E9ECF2");
        SetBrush(resources, "ControlFillColorTertiaryBrush", dark ? "#3C424D" : "#E2E6ED");
        SetBrush(resources, "ControlFillColorInputActiveBrush", dark ? "#30353E" : "#FFFFFF");
        SetBrush(resources, "ControlStrokeColorDefaultBrush", dark ? "#484F5B" : "#D4D9E2");
        SetBrush(resources, "DividerStrokeColorDefaultBrush", dark ? "#383E47" : "#E0E4EA");
        SetBrush(resources, "SurfaceStrokeColorDefaultBrush", dark ? "#555C68" : "#C8CED8");
        SetBrush(resources, "TextFillColorPrimaryBrush", dark ? "#F4F5F7" : "#202124");
        SetBrush(resources, "TextFillColorSecondaryBrush", dark ? "#C8CCD3" : "#5F6368");
        SetBrush(resources, "TextFillColorTertiaryBrush", dark ? "#9298A2" : "#7A7F87");
        SetBrush(resources, "LayerFillColorDefaultBrush", dark ? "#202329" : "#EEF1F5");
        SetBrush(resources, "LayerFillColorAltBrush", dark ? "#292D35" : "#FFFFFF");
        SetBrush(resources, "SolidBackgroundFillColorBaseBrush", dark ? "#181A1F" : "#F6F7FB");
        SetBrush(resources, "SolidBackgroundFillColorSecondaryBrush", dark ? "#202329" : "#FFFFFF");
    }

    private static void ClearLauncherPalette(ResourceDictionary resources)
    {
        foreach (var key in PaletteKeys)
        {
            resources.Remove(key);
        }
    }

    private static void SetBrush(ResourceDictionary resources, string key, string hex)
    {
        var color = (Color)ColorConverter.ConvertFromString(hex);
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        resources[key] = brush;
    }

    private void StartWatchingSystemTheme()
    {
        if (_watchingSystemTheme || _window is null || !_window.IsLoaded)
        {
            return;
        }

        SystemThemeWatcher.Watch(_window, WindowBackdropType.None);
        _watchingSystemTheme = true;
    }

    private void StopWatchingSystemTheme()
    {
        if (!_watchingSystemTheme || _window is null)
        {
            return;
        }

        if (_window.IsLoaded)
        {
            SystemThemeWatcher.UnWatch(_window);
        }

        _watchingSystemTheme = false;
    }
}
