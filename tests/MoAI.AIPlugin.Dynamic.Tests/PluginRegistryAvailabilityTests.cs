using MoAI.AIPlugin.Dynamic.Plugins;
using MoAI.AIPlugin.Services;
using Xunit;

namespace MoAI.AIPlugin.Dynamic.Tests;

public class PluginRegistryAvailabilityTests
{
    [Fact]
    public void SetEnabled_OfflinesPluginWithoutRemovingItsRegistration()
    {
        var registry = new PluginRegistry();
        var assembly = typeof(DynamicGreetPlugin).Assembly;
        registry.RegisterAssembly(assembly, enabled: false);

        Assert.True(registry.IsRegistered("dynamic_greet"));
        Assert.Null(registry.Get("dynamic_greet"));
        Assert.DoesNotContain(registry.GetAll(), plugin => plugin.Key == "dynamic_greet");

        registry.SetEnabled("dynamic_greet", enabled: true);
        Assert.NotNull(registry.Get("dynamic_greet"));

        registry.SetEnabled("dynamic_greet", enabled: false);
        Assert.True(registry.IsRegistered("dynamic_greet"));
        Assert.Null(registry.Get("dynamic_greet"));
    }
}
