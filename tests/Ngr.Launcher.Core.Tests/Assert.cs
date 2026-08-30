namespace Ngr.Launcher.Core.Tests;

// Temporary compatibility wrapper for the xUnit v2 assertion surface. Remove
// after ConfigurationStoreTests can be patched to use Assert.False(File.Exists()).
internal static class Assert
{
    public static void Contains<T>(T expected, IEnumerable<T> collection) =>
        global::Xunit.Assert.Contains(expected, collection);

    public static void Contains<T>(IEnumerable<T> collection, Predicate<T> filter) =>
        global::Xunit.Assert.Contains(collection, filter);

    public static void DoesNotContain<T>(T expected, IEnumerable<T> collection) =>
        global::Xunit.Assert.DoesNotContain(expected, collection);

    public static void Empty(IEnumerable collection) => global::Xunit.Assert.Empty(collection);

    public static void NotEmpty(IEnumerable collection) => global::Xunit.Assert.NotEmpty(collection);

    public static void True(bool condition) => global::Xunit.Assert.True(condition);

    public static void False(bool condition) => global::Xunit.Assert.False(condition);

    public static void Equal<T>(T expected, T actual) => global::Xunit.Assert.Equal(expected, actual);

    public static T Throws<T>(Action action)
        where T : Exception => global::Xunit.Assert.Throws<T>(action);

    public static void DoesNotExist(string path) => global::Xunit.Assert.False(File.Exists(path));
}
