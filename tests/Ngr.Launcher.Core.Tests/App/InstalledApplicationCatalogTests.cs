using Ngr.Launcher.App.Services;
using Xunit;

namespace Ngr.Launcher.Core.Tests.App;

public sealed class InstalledApplicationCatalogTests
{
    [Fact]
    public void Discovers_start_menu_shortcuts_before_file_browse_fallback()
    {
        var root = Directory.CreateTempSubdirectory("ngr-launcher-app-catalog-").FullName;
        try
        {
            File.WriteAllText(Path.Combine(root, "Visual Studio Code.lnk"), string.Empty);
            var nested = Directory.CreateDirectory(Path.Combine(root, "Browsers")).FullName;
            File.WriteAllText(Path.Combine(nested, "Google Chrome.lnk"), string.Empty);
            File.WriteAllText(Path.Combine(root, "Uninstall Sample.lnk"), string.Empty);

            var catalog = new WindowsInstalledApplicationCatalog([root], includeRegistry: false);
            var entries = catalog.Discover();

            Assert.Contains(entries, entry => entry.Name == "Visual Studio Code" && entry.Source == "Start menu");
            Assert.Contains(entries, entry => entry.Name == "Google Chrome" && entry.Source == "Start menu");
            Assert.DoesNotContain(entries, entry => entry.Name.Contains("Uninstall", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
