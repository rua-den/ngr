using System.Windows;
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
    private Window? _window;
    private ThemePreference _currentTheme = ThemePreference.System;
    private bool _watchingSystemTheme;

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
