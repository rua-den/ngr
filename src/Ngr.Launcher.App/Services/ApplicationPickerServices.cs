using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;

namespace Ngr.Launcher.App.Services;

public sealed record InstalledApplicationEntry(string Name, string Target, string Source)
{
    public string DisplayText => $"{Name}  ·  {Source}";
}

public interface IInstalledApplicationCatalog
{
    IReadOnlyList<InstalledApplicationEntry> Discover();
}

public sealed class WindowsInstalledApplicationCatalog : IInstalledApplicationCatalog
{
    private readonly IReadOnlyList<string> _startMenuRoots;
    private readonly bool _includeRegistry;

    public WindowsInstalledApplicationCatalog()
        : this(GetDefaultStartMenuRoots(), includeRegistry: true)
    {
    }

    public WindowsInstalledApplicationCatalog(
        IEnumerable<string> startMenuRoots,
        bool includeRegistry)
    {
        ArgumentNullException.ThrowIfNull(startMenuRoots);
        _startMenuRoots = startMenuRoots
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        _includeRegistry = includeRegistry;
    }

    public IReadOnlyList<InstalledApplicationEntry> Discover()
    {
        var byTarget = new Dictionary<string, InstalledApplicationEntry>(StringComparer.OrdinalIgnoreCase);

        foreach (var root in _startMenuRoots)
        {
            AddStartMenuEntries(root, byTarget);
        }

        if (_includeRegistry)
        {
            AddRegistryEntries(byTarget);
        }

        return byTarget.Values
            .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Source, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<string> GetDefaultStartMenuRoots()
    {
        return
        [
            Environment.GetFolderPath(Environment.SpecialFolder.Programs),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms)
        ];
    }

    private static void AddStartMenuEntries(
        string root,
        IDictionary<string, InstalledApplicationEntry> byTarget)
    {
        if (!Directory.Exists(root))
        {
            return;
        }

        foreach (var file in EnumerateFilesSafe(root))
        {
            var extension = Path.GetExtension(file);
            if (!extension.Equals(".lnk", StringComparison.OrdinalIgnoreCase)
                && !extension.Equals(".url", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var name = Path.GetFileNameWithoutExtension(file).Trim();
            if (name.Length == 0 || LooksLikeMaintenanceShortcut(name))
            {
                continue;
            }

            var target = Path.GetFullPath(file);
            byTarget.TryAdd(
                target,
                new InstalledApplicationEntry(name, target, "Start menu"));
        }
    }

    private static IEnumerable<string> EnumerateFilesSafe(string root)
    {
        var pending = new Stack<string>();
        pending.Push(root);

        while (pending.Count > 0)
        {
            var directory = pending.Pop();

            string[] files;
            try
            {
                files = Directory.GetFiles(directory);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var file in files)
            {
                yield return file;
            }

            string[] childDirectories;
            try
            {
                childDirectories = Directory.GetDirectories(directory);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var child in childDirectories)
            {
                pending.Push(child);
            }
        }
    }

    private static bool LooksLikeMaintenanceShortcut(string name)
    {
        return name.Contains("uninstall", StringComparison.OrdinalIgnoreCase)
            || name.Contains("readme", StringComparison.OrdinalIgnoreCase)
            || name.Contains("release notes", StringComparison.OrdinalIgnoreCase)
            || name.Contains("help", StringComparison.OrdinalIgnoreCase);
    }

    private static void AddRegistryEntries(
        IDictionary<string, InstalledApplicationEntry> byTarget)
    {
        foreach (var (hive, view) in RegistryLocations())
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(hive, view);
                using var appPaths = baseKey.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\App Paths",
                    writable: false);
                if (appPaths is null)
                {
                    continue;
                }

                foreach (var subKeyName in appPaths.GetSubKeyNames())
                {
                    try
                    {
                        using var appKey = appPaths.OpenSubKey(subKeyName, writable: false);
                        var rawPath = appKey?.GetValue(null) as string;
                        var path = NormalizeExecutablePath(rawPath);
                        if (path is null || !File.Exists(path))
                        {
                            continue;
                        }

                        var name = GetExecutableDisplayName(path, subKeyName);
                        byTarget.TryAdd(
                            path,
                            new InstalledApplicationEntry(name, path, "Windows app registration"));
                    }
                    catch (Exception exception) when (
                        exception is IOException
                        or UnauthorizedAccessException
                        or System.Security.SecurityException)
                    {
                    }
                }
            }
            catch (Exception exception) when (
                exception is IOException
                or UnauthorizedAccessException
                or System.Security.SecurityException
                or PlatformNotSupportedException)
            {
            }
        }
    }

    private static IEnumerable<(RegistryHive Hive, RegistryView View)> RegistryLocations()
    {
        yield return (RegistryHive.CurrentUser, RegistryView.Default);
        yield return (RegistryHive.LocalMachine, RegistryView.Registry64);
        yield return (RegistryHive.LocalMachine, RegistryView.Registry32);
    }

    private static string? NormalizeExecutablePath(string? rawPath)
    {
        if (string.IsNullOrWhiteSpace(rawPath))
        {
            return null;
        }

        var candidate = Environment.ExpandEnvironmentVariables(rawPath.Trim().Trim('"'));
        try
        {
            return Path.GetFullPath(candidate);
        }
        catch (Exception exception) when (
            exception is ArgumentException
            or NotSupportedException
            or PathTooLongException)
        {
            return null;
        }
    }

    private static string GetExecutableDisplayName(string path, string fallback)
    {
        try
        {
            var version = FileVersionInfo.GetVersionInfo(path);
            var description = version.FileDescription?.Trim();
            if (!string.IsNullOrWhiteSpace(description))
            {
                return description;
            }

            var product = version.ProductName?.Trim();
            if (!string.IsNullOrWhiteSpace(product))
            {
                return product;
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }

        return Path.GetFileNameWithoutExtension(fallback);
    }
}

internal sealed class InstalledApplicationPickerWindow : Window
{
    private readonly IReadOnlyList<InstalledApplicationEntry> _allApplications;
    private readonly TextBox _searchBox;
    private readonly ListBox _applicationList;
    private readonly TextBlock _summaryText;
    private readonly TextBlock _selectedTargetText;

    public InstalledApplicationPickerWindow(
        IReadOnlyList<InstalledApplicationEntry> applications,
        string? currentTarget)
    {
        _allApplications = applications ?? throw new ArgumentNullException(nameof(applications));

        Title = "Choose installed app";
        Width = 720;
        Height = 580;
        MinWidth = 560;
        MinHeight = 420;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.CanResizeWithGrip;
        SetResourceReference(BackgroundProperty, "ApplicationBackgroundBrush");
        SetResourceReference(ForegroundProperty, "TextFillColorPrimaryBrush");

        _searchBox = new TextBox
        {
            MinHeight = 38,
            Margin = new Thickness(0, 0, 0, 10)
        };
        _searchBox.TextChanged += (_, _) => RefreshList();

        _applicationList = new ListBox
        {
            DisplayMemberPath = nameof(InstalledApplicationEntry.DisplayText),
            HorizontalContentAlignment = HorizontalAlignment.Stretch
        };
        _applicationList.SelectionChanged += (_, _) => UpdateSelectedTarget();
        _applicationList.MouseDoubleClick += (_, _) => UseSelection();

        _summaryText = CreateSecondaryText();
        _selectedTargetText = CreateSecondaryText();
        _selectedTargetText.TextWrapping = TextWrapping.Wrap;

        Content = BuildContent();
        PreviewKeyDown += OnPreviewKeyDown;
        Loaded += (_, _) =>
        {
            RefreshList();
            if (!string.IsNullOrWhiteSpace(currentTarget))
            {
                _applicationList.SelectedItem = _allApplications.FirstOrDefault(entry =>
                    string.Equals(entry.Target, currentTarget, StringComparison.OrdinalIgnoreCase));
                _applicationList.ScrollIntoView(_applicationList.SelectedItem);
            }

            _searchBox.Focus();
        };
    }

    public string? SelectedTarget { get; private set; }

    public bool BrowseFileRequested { get; private set; }

    private UIElement BuildContent()
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var titleBar = new Border
        {
            Height = 44,
            Padding = new Thickness(14, 0, 8, 0),
            BorderThickness = new Thickness(0, 0, 0, 1)
        };
        titleBar.SetResourceReference(Border.BackgroundProperty, "CardBackgroundFillColorSecondaryBrush");
        titleBar.SetResourceReference(Border.BorderBrushProperty, "DividerStrokeColorDefaultBrush");
        titleBar.MouseLeftButtonDown += (_, eventArgs) =>
        {
            if (eventArgs.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        };

        var titleGrid = new Grid();
        titleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        titleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        titleGrid.Children.Add(new TextBlock
        {
            Text = "Choose installed app",
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        });

        var closeButton = CreateButton("Close", "ToolbarButtonStyle");
        closeButton.Padding = new Thickness(12, 4);
        closeButton.Click += (_, _) => Close();
        Grid.SetColumn(closeButton, 1);
        titleGrid.Children.Add(closeButton);
        titleBar.Child = titleGrid;
        root.Children.Add(titleBar);

        var panel = new Grid
        {
            Margin = new Thickness(18)
        };
        panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        panel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var heading = new StackPanel { Margin = new Thickness(0, 0, 0, 14) };
        heading.Children.Add(new TextBlock
        {
            Text = "Installed apps",
            FontSize = 20,
            FontWeight = FontWeights.SemiBold
        });
        var help = CreateSecondaryText();
        help.Text = "Search the apps Windows already exposes in the Start menu. If the app is missing, use Browse file instead.";
        help.Margin = new Thickness(0, 5, 0, 0);
        heading.Children.Add(help);
        panel.Children.Add(heading);

        var searchPanel = new Grid();
        searchPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        searchPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _searchBox.ToolTip = "Search installed apps";
        searchPanel.Children.Add(_searchBox);
        _summaryText.Margin = new Thickness(12, 0, 0, 10);
        _summaryText.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(_summaryText, 1);
        searchPanel.Children.Add(_summaryText);
        Grid.SetRow(searchPanel, 1);
        panel.Children.Add(searchPanel);

        var listBorder = new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(4)
        };
        listBorder.SetResourceReference(Border.BackgroundProperty, "CardBackgroundFillColorDefaultBrush");
        listBorder.SetResourceReference(Border.BorderBrushProperty, "ControlStrokeColorDefaultBrush");
        listBorder.Child = _applicationList;
        Grid.SetRow(listBorder, 2);
        panel.Children.Add(listBorder);

        var selectedPanel = new StackPanel { Margin = new Thickness(2, 10, 2, 12) };
        var selectedLabel = new TextBlock
        {
            Text = "Selected target",
            FontWeight = FontWeights.SemiBold,
            FontSize = 11
        };
        selectedPanel.Children.Add(selectedLabel);
        _selectedTargetText.Margin = new Thickness(0, 4, 0, 0);
        selectedPanel.Children.Add(_selectedTargetText);
        Grid.SetRow(selectedPanel, 3);
        panel.Children.Add(selectedPanel);

        var actions = new Grid();
        actions.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        actions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        actions.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        actions.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var browseButton = CreateButton("Browse file instead…", "ToolbarButtonStyle");
        browseButton.Click += (_, _) =>
        {
            BrowseFileRequested = true;
            DialogResult = false;
        };
        actions.Children.Add(browseButton);

        var cancelButton = CreateButton("Cancel", "ToolbarButtonStyle");
        cancelButton.Margin = new Thickness(0, 0, 9, 0);
        cancelButton.Click += (_, _) => DialogResult = false;
        Grid.SetColumn(cancelButton, 2);
        actions.Children.Add(cancelButton);

        var useButton = CreateButton("Use selected app", "PrimaryButtonStyle");
        useButton.IsDefault = true;
        useButton.Click += (_, _) => UseSelection();
        Grid.SetColumn(useButton, 3);
        actions.Children.Add(useButton);

        Grid.SetRow(actions, 4);
        panel.Children.Add(actions);

        Grid.SetRow(panel, 1);
        root.Children.Add(panel);
        return root;
    }

    private void RefreshList()
    {
        var query = _searchBox.Text.Trim();
        var filtered = _allApplications
            .Where(entry => query.Length == 0
                || entry.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                || entry.Target.Contains(query, StringComparison.OrdinalIgnoreCase)
                || entry.Source.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        _applicationList.ItemsSource = filtered;
        _summaryText.Text = filtered.Length == 1 ? "1 app" : $"{filtered.Length} apps";
        if (filtered.Length == 0)
        {
            _selectedTargetText.Text = "No matching app. Use Browse file instead if Windows does not expose it here.";
        }
    }

    private void UpdateSelectedTarget()
    {
        _selectedTargetText.Text = _applicationList.SelectedItem is InstalledApplicationEntry entry
            ? entry.Target
            : "Select an app from the list.";
    }

    private void UseSelection()
    {
        if (_applicationList.SelectedItem is not InstalledApplicationEntry entry)
        {
            return;
        }

        SelectedTarget = entry.Target;
        DialogResult = true;
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DialogResult = false;
            e.Handled = true;
        }
    }

    private static Button CreateButton(string text, string styleKey)
    {
        var button = new Button
        {
            Content = text,
            Padding = new Thickness(14, 6)
        };
        if (Application.Current.TryFindResource(styleKey) is Style style)
        {
            button.Style = style;
        }

        return button;
    }

    private static TextBlock CreateSecondaryText()
    {
        var text = new TextBlock();
        text.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");
        return text;
    }
}
