using UptimeWidget.Items;
using UptimeWidget.Models;
using Xunit;

namespace UptimeWidget.Tests;

public sealed class SourceTypeRegistryTests
{
    [Fact]
    public void FindReturnsRegisteredSourceTypeAndNullForUnknownId()
    {
        SourceType? uptime = SourceTypeRegistry.Find(SourceTypeRegistry.UptimeTypeId);

        Assert.NotNull(uptime);
        Assert.Equal("System uptime", uptime.DisplayName);
        Assert.Null(SourceTypeRegistry.Find("unknown"));
        Assert.Null(SourceTypeRegistry.Find(null));
    }

    [Fact]
    public void UptimeSourceCreatesItemWithInstanceIdentityAndDisplayName()
    {
        SourceType uptime = SourceTypeRegistry.Find(SourceTypeRegistry.UptimeTypeId)!;
        SourceInstance instance = new()
        {
            Id = "custom-uptime",
            TypeId = SourceTypeRegistry.UptimeTypeId,
            DisplayName = "My computer",
        };

        IWidgetItem item = uptime.Create(instance);

        Assert.Equal("custom-uptime", item.Id);
        Assert.Equal("My computer", item.Name);
        Assert.StartsWith("System: ", item.GetDisplayText());
    }

    [Fact]
    public void RequiredParameterValidationTrimsInput()
    {
        ParameterDescriptor parameter = new(
            key: "processName",
            label: "Process",
            kind: ParameterKind.Choice,
            required: true);

        Assert.Equal("Process is required.", parameter.Validate("  "));
        Assert.Null(parameter.Validate("  explorer  "));
    }
}
