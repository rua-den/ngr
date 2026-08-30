using Ngr.Launcher.Core.Configuration;
using Xunit;

namespace Ngr.Launcher.Core.Tests.Configuration;

public sealed class ConfigurationStoreConcurrencyTests
{
    [Fact]
    public async Task Concurrent_saves_share_no_temp_file_race_and_leave_valid_configuration()
    {
        var directory = Directory.CreateTempSubdirectory("ngr-launcher-config-concurrency-").FullName;
        try
        {
            var store = new JsonConfigurationStore(directory);
            var configuration = AppConfiguration.CreateDefault();

            await Task.WhenAll(Enumerable.Range(0, 32)
                .Select(_ => store.SaveAsync(configuration)));

            var loaded = await store.LoadAsync();
            Assert.Equal(configuration, loaded.Configuration);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
