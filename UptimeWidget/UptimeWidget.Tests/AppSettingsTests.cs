using UptimeWidget.Models;
using Xunit;

namespace UptimeWidget.Tests;

public sealed class AppSettingsTests
{
    [Fact]
    public void CloneCopiesSettingsAndDeepCopiesMutableCollections()
    {
        AppSettings original = new()
        {
            EnabledItems = ["source-1"],
            Sources =
            [
                new SourceInstance
                {
                    Id = "source-1",
                    TypeId = SourceTypeRegistry.ProcessTypeId,
                    DisplayName = "Browser",
                    Parameters = new Dictionary<string, string> { ["processName"] = "browser" },
                },
            ],
            WidgetVisible = false,
            HideNonRunningProcesses = true,
        };

        AppSettings clone = original.Clone();
        clone.EnabledItems.Add("another-source");
        clone.Sources[0].Parameters["processName"] = "another-process";

        Assert.Equal(["source-1"], original.EnabledItems);
        Assert.Equal("browser", original.Sources[0].Parameters["processName"]);
        Assert.False(clone.WidgetVisible);
        Assert.True(clone.HideNonRunningProcesses);
    }
}
