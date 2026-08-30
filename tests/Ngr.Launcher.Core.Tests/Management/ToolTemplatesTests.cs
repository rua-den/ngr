using Ngr.Launcher.Core.Configuration;
using Ngr.Launcher.Core.Management;
using Xunit;

namespace Ngr.Launcher.Core.Tests.Management;

public sealed class ToolTemplatesTests
{
    [Fact]
    public void Built_in_templates_are_unique_editable_and_domain_valid()
    {
        Assert.Equal(6, ToolTemplates.All.Count);
        Assert.Equal(
            ToolTemplates.All.Count,
            ToolTemplates.All.Select(template => template.Key).Distinct(StringComparer.OrdinalIgnoreCase).Count());

        foreach (var template in ToolTemplates.All)
        {
            Assert.Empty(ToolValidator.Validate(template.Definition));
        }
    }

    [Fact]
    public void Create_returns_an_independent_definition()
    {
        var first = ToolTemplates.Create("npm");
        var second = ToolTemplates.Create("npm");

        Assert.NotSame(first.EnvironmentVariables, second.EnvironmentVariables);
        Assert.Equal(first, second);
    }
}
