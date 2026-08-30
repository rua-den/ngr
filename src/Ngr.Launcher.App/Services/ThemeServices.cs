using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
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
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaUseImmersiveDarkModeLegacy = 19;
    private const int DwmwaCaptionColor = 35;
    private const int DwmwaTextColor = 36;

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
        ApplyNativeWindowTheme(ApplicationThemeManager.GetAppTheme());

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

        var appTheme = ApplicationThemeManager.GetAppTheme();
        ApplyLauncherPalette(appTheme);
        ApplyNativeWindowTheme(appTheme);
    }

    private void OnApplicationThemeChanged(ApplicationTheme theme, Color accent)
    {
        ApplyLauncherPalette(theme);
        ApplyNativeWindowTheme(theme);
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

        // Keep each workbench plane visibly separate, like a desktop IDE: the
        // editor canvas, sidebar, title/activity chrome, controls and cards use
        // distinct opaque surfaces instead of translucent brushes that collapse
        // into one flat block in dark mode.
        SetBrush(resources, "ApplicationBackgroundBrush", dark ? "#17191D" : "#F2F4F8");
        SetBrush(resources, "CardBackgroundFillColorDefaultBrush", dark ? "#252930" : "#FFFFFF");
        SetBrush(resources, "CardBackgroundFillColorSecondaryBrush", dark ? "#111318" : "#E9EDF3");
        SetBrush(resources, "ControlFillColorDefaultBrush", dark ? "#303640" : "#F5F7FA");
        SetBrush(resources, "ControlFillColorSecondaryBrush", dark ? "#39414C" : "#E8ECF2");
        SetBrush(resources, "ControlFillColorTertiaryBrush", dark ? "#434C59" : "#DEE3EA");
        SetBrush(resources, "ControlFillColorInputActiveBrush", dark ? "#343B46" : "#FFFFFF");
        SetBrush(resources, "ControlStrokeColorDefaultBrush", dark ? "#596270" : "#C8CFD9");
        SetBrush(resources, "DividerStrokeColorDefaultBrush", dark ? "#414956" : "#CBD2DC");
        SetBrush(resources, "SurfaceStrokeColorDefaultBrush", dark ? "#687281" : "#B9C1CD");
        SetBrush(resources, "TextFillColorPrimaryBrush", dark ? "#F4F5F7" : "#202124");
        SetBrush(resources, "TextFillColorSecondaryBrush", dark ? "#C9CED6" : "#565B63");
        SetBrush(resources, "TextFillColorTertiaryBrush", dark ? "#969EAA" : "#747A84");
        SetBrush(resources, "LayerFillColorDefaultBrush", dark ? "#20242B" : "#EDF0F4");
        SetBrush(resources, "LayerFillColorAltBrush", dark ? "#2B3038" : "#FFFFFF");
        SetBrush(resources, "SolidBackgroundFillColorBaseBrush", dark ? "#17191D" : "#F2F4F8");
        SetBrush(resources, "SolidBackgroundFillColorSecondaryBrush", dark ? "#20242B" : "#FFFFFF");
    }

    private void ApplyNativeWindowTheme(ApplicationTheme theme)
    {
        if (_window is null || !_window.IsLoaded || theme == ApplicationTheme.HighContrast)
        {
            return;
        }

        var handle = new WindowInteropHelper(_window).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        var dark = theme == ApplicationTheme.Dark;
        var enabled = dark ? 1 : 0;

        // Attribute 20 is the documented value. Attribute 19 keeps the same
        // behavior working on older Windows 10 builds where the value differed.
        var result = DwmSetWindowAttribute(handle, DwmwaUseImmersiveDarkMode, ref enabled, sizeof(int));
        if (result != 0)
        {
            _ = DwmSetWindowAttribute(handle, DwmwaUseImmersiveDarkModeLegacy, ref enabled, sizeof(int));
        }

        // Windows 11 supports explicit caption/text colors. Older Windows
        // versions simply reject these attributes, so the immersive dark/light
        // flag above remains the safe fallback there.
        var captionColor = ToColorRef(dark ? "#111318" : "#E9EDF3");
        var textColor = ToColorRef(dark ? "#F4F5F7" : "#202124");
        _ = DwmSetWindowAttribute(handle, DwmwaCaptionColor, ref captionColor, sizeof(int));
        _ = DwmSetWindowAttribute(handle, DwmwaTextColor, ref textColor, sizeof(int));
    }

    private static int ToColorRef(string hex)
    {
        var color = (Color)ColorConverter.ConvertFromString(hex);
        return color.R | (color.G << 8) | (color.B << 16);
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

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(
        IntPtr hwnd,
        int attribute,
        ref int attributeValue,
        int attributeSize);
}
