namespace Ngr.Launcher.Core.Configuration;

// Temporary compatibility shim while the workspace patch helper cannot update
// UnsupportedSchemaVersionException's base type in AppConfiguration.cs.
public class InvalidDataException : Exception
{
    public InvalidDataException(string message)
        : base(message)
    {
    }
}
