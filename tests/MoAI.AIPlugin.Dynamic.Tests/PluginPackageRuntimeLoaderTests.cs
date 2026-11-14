using System.Text.Json;
using MoAI.AIPlugin.Services;
using Xunit;

namespace MoAI.AIPlugin.Dynamic.Tests;

public class PluginPackageRuntimeLoaderTests
{
    [Fact]
    public void DeserializeMetadata_ReadsCamelCaseInstallSnapshot()
    {
        var expected = new InstalledPluginMetadata
        {
            PluginKey = "demo.search",
            PackageSha256 = new string('a', 64),
            EntryAssembly = "Demo.Search.dll",
            Enabled = true,
        };
        var json = JsonSerializer.Serialize(expected, JsonSerializerOptions.Web);

        var actual = PluginPackageRuntimeLoader.DeserializeMetadata(json);

        Assert.Equal(expected.PluginKey, actual.PluginKey);
        Assert.Equal(expected.PackageSha256, actual.PackageSha256);
        Assert.Equal(expected.EntryAssembly, actual.EntryAssembly);
        Assert.True(actual.Enabled);
    }

    [Fact]
    public void EnumeratePackageDirectories_FindsPluginFoldersAndSkipsHiddenStagingFolders()
    {
        var root = Directory.CreateTempSubdirectory("moai-plugin-startup-");
        try
        {
            Directory.CreateDirectory(Path.Combine(root.FullName, "demo.search"));
            Directory.CreateDirectory(Path.Combine(root.FullName, "another_plugin"));
            Directory.CreateDirectory(Path.Combine(root.FullName, ".staging-interrupted"));

            var directories = PluginMarketplacePaths.EnumeratePackageDirectories(root.FullName)
                .Select(directory => Path.GetFileName(directory)!)
                .ToArray();

            Assert.Equal(["another_plugin", "demo.search"], directories);
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }
}
