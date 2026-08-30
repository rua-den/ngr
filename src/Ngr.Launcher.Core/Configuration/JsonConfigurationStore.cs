using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ngr.Launcher.Core.Configuration;

public sealed record ConfigurationLoadResult(
    AppConfiguration Configuration,
    IReadOnlyList<string> Warnings);

public sealed class JsonConfigurationStore
{
    private const string ConfigurationFileName = "config.json";
    private const string BackupFileName = "config.json.bak";
    private const string TemporaryFileName = "config.json.tmp";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _dataDirectory;
    private readonly string _configurationPath;
    private readonly string _backupPath;
    private readonly string _temporaryPath;

    public JsonConfigurationStore(string dataDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
        _dataDirectory = dataDirectory;
        _configurationPath = Path.Combine(dataDirectory, ConfigurationFileName);
        _backupPath = Path.Combine(dataDirectory, BackupFileName);
        _temporaryPath = Path.Combine(dataDirectory, TemporaryFileName);
    }

    public async Task SaveAsync(
        AppConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        AppConfigurationValidator.Validate(configuration);
        Directory.CreateDirectory(_dataDirectory);

        try
        {
            await using (var stream = new FileStream(
                _temporaryPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(
                    stream,
                    configuration,
                    SerializerOptions,
                    cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            if (File.Exists(_configurationPath))
            {
                File.Delete(_backupPath);
                File.Replace(_temporaryPath, _configurationPath, _backupPath, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(_temporaryPath, _configurationPath);
            }
        }
        finally
        {
            File.Delete(_temporaryPath);
        }
    }

    public async Task<ConfigurationLoadResult> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_configurationPath))
        {
            return new ConfigurationLoadResult(AppConfiguration.CreateDefault(), Array.Empty<string>());
        }

        try
        {
            var configuration = await ReadAndValidateAsync(_configurationPath, cancellationToken);
            return new ConfigurationLoadResult(configuration, Array.Empty<string>());
        }
        catch (UnsupportedSchemaVersionException)
        {
            throw;
        }
        catch (Exception exception) when (IsRecoverableReadFailure(exception))
        {
            if (File.Exists(_backupPath))
            {
                try
                {
                    var backup = await ReadAndValidateAsync(_backupPath, cancellationToken);
                    return new ConfigurationLoadResult(
                        backup,
                        new[] { "The main configuration was invalid; the backup configuration was loaded." });
                }
                catch (UnsupportedSchemaVersionException)
                {
                    throw;
                }
                catch (Exception backupException) when (IsRecoverableReadFailure(backupException))
                {
                    // Both files are preserved below for diagnosis.
                }
            }

            var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssfff");
            PreserveCorruptFile(_configurationPath, $"config.json.corrupt-{timestamp}");
            PreserveCorruptFile(_backupPath, $"config.json.bak.corrupt-{timestamp}");

            return new ConfigurationLoadResult(
                AppConfiguration.CreateDefault(),
                new[] { "Configuration and backup were invalid; a new empty configuration was created." });
        }
    }

    private static bool IsRecoverableReadFailure(Exception exception) =>
        exception is JsonException or IOException or InvalidDataException;

    private static void PreserveCorruptFile(string sourcePath, string destinationFileName)
    {
        if (!File.Exists(sourcePath))
        {
            return;
        }

        var destinationPath = Path.Combine(Path.GetDirectoryName(sourcePath)!, destinationFileName);
        File.Move(sourcePath, destinationPath);
    }

    private static async Task<AppConfiguration> ReadAndValidateAsync(
        string path,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        var configuration = await JsonSerializer.DeserializeAsync<AppConfiguration>(
            stream,
            SerializerOptions,
            cancellationToken);

        if (configuration is null)
        {
            throw new InvalidDataException("Configuration is empty.");
        }

        AppConfigurationValidator.Validate(configuration);
        return configuration;
    }
}
