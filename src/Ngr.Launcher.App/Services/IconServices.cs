using System.Collections.Concurrent;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Ngr.Launcher.Core.Models;

namespace Ngr.Launcher.App.Services;

public static class LauncherIconProvider
{
    private static readonly Lazy<ImageSource> CachedIcon = new(CreateIcon);

    public static ImageSource GetIcon() => CachedIcon.Value;

    private static ImageSource CreateIcon()
    {
        var frame = BitmapFrame.Create(
            new Uri("pack://application:,,,/Assets/NgrLauncher.ico", UriKind.Absolute),
            BitmapCreateOptions.PreservePixelFormat,
            BitmapCacheOption.OnLoad);
        if (frame.CanFreeze)
        {
            frame.Freeze();
        }

        return frame;
    }
}

public sealed class ToolIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ToolDefinition { Kind: ToolKind.Application, Target: { Length: > 0 } target })
        {
            return WindowsShellIconProvider.GetSmallIcon(target) ?? LauncherIconProvider.GetIcon();
        }

        return LauncherIconProvider.GetIcon();
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public static class WindowsShellIconProvider
{
    private const uint ShgfiIcon = 0x000000100;
    private const uint ShgfiSmallIcon = 0x000000001;
    private static readonly ConcurrentDictionary<string, ImageSource> Cache =
        new(StringComparer.OrdinalIgnoreCase);

    public static ImageSource? GetSmallIcon(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var fullPath = path;
        try
        {
            fullPath = System.IO.Path.GetFullPath(path);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or System.IO.PathTooLongException)
        {
        }

        if (Cache.TryGetValue(fullPath, out var cached))
        {
            return cached;
        }

        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        var result = SHGetFileInfo(
            fullPath,
            0,
            out var info,
            (uint)Marshal.SizeOf<ShFileInfo>(),
            ShgfiIcon | ShgfiSmallIcon);
        if (result == IntPtr.Zero || info.IconHandle == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            var source = Imaging.CreateBitmapSourceFromHIcon(
                info.IconHandle,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            if (source.CanFreeze)
            {
                source.Freeze();
            }

            Cache.TryAdd(fullPath, source);
            return source;
        }
        finally
        {
            _ = DestroyIcon(info.IconHandle);
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(
        string path,
        uint fileAttributes,
        out ShFileInfo fileInfo,
        uint fileInfoSize,
        uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr iconHandle);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShFileInfo
    {
        public IntPtr IconHandle;
        public int IconIndex;
        public uint Attributes;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string DisplayName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string TypeName;
    }
}
