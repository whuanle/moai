using System.IO.Compression;
using System.Text;
using MoAI.AIPlugin.Services;
using MoAI.Infra.Exceptions;
using Xunit;

namespace MoAI.AIPlugin.Dynamic.Tests;

public class PluginPackageArchiveTests
{
    [Fact]
    public async Task ExtractAsync_WrappedPackage_ShouldFlattenRootAndReadDescriptor()
    {
        using var temp = new TemporaryDirectory();
        var archivePath = Path.Combine(temp.Path, "plugin.zip");
        CreateZip(archivePath,
        [
            ("Wrapper/plugin.json", "{\"key\":\"demo.search\",\"version\":\"1.2.0\",\"name\":\"Demo Search\",\"summary\":\"Search for people\",\"description\":\"AI: search public web pages and return source links\"}"),
            ("Wrapper/Demo.Search.dll", "not loaded by installer"),
        ]);
        var staging = Path.Combine(temp.Path, "staging");

        var result = await PluginPackageArchive.ExtractAsync(archivePath, staging, new MoPlatformPluginManifest
        {
            Key = "demo.search",
            ManifestKey = "demo.search",
            Version = "1.2.0",
            TargetFramework = "net10.0",
            RootPath = "Wrapper",
            EntryAssemblyPath = "Demo.Search.dll",
        }, "demo.search", CancellationToken.None);

        Assert.Equal("Demo Search", result.Name);
        Assert.Equal("Search for people", result.Summary);
        Assert.Equal("AI: search public web pages and return source links", result.Description);
        Assert.Equal("Demo.Search.dll", result.EntryAssemblyPath);
        Assert.True(File.Exists(Path.Combine(staging, "Demo.Search.dll")));
    }

    [Fact]
    public async Task ExtractAsync_PathTraversal_ShouldRejectBeforeWritingOutsideStaging()
    {
        using var temp = new TemporaryDirectory();
        var archivePath = Path.Combine(temp.Path, "unsafe.zip");
        CreateZip(archivePath, [("../escape.txt", "unsafe")]);
        var staging = Path.Combine(temp.Path, "staging");

        var exception = await Assert.ThrowsAsync<BusinessException>(() => PluginPackageArchive.ExtractAsync(
            archivePath,
            staging,
            new MoPlatformPluginManifest { RootPath = string.Empty },
            "demo.search",
            CancellationToken.None));

        Assert.Equal(400, exception.StatusCode);
        Assert.False(File.Exists(Path.Combine(temp.Path, "escape.txt")));
    }

    [Fact]
    public async Task ExtractAsync_MismatchedDescriptorKey_ShouldReject()
    {
        using var temp = new TemporaryDirectory();
        var archivePath = Path.Combine(temp.Path, "mismatch.zip");
        CreateZip(archivePath,
        [
            ("plugin.json", "{\"key\":\"other.plugin\",\"version\":\"1.2.0\"}"),
            ("Demo.dll", "placeholder"),
        ]);

        var exception = await Assert.ThrowsAsync<BusinessException>(() => PluginPackageArchive.ExtractAsync(
            archivePath,
            Path.Combine(temp.Path, "staging"),
            new MoPlatformPluginManifest { RootPath = string.Empty, EntryAssemblyPath = "Demo.dll", Version = "1.2.0" },
            "demo.search",
            CancellationToken.None));

        Assert.Equal(400, exception.StatusCode);
    }

    private static void CreateZip(string path, IReadOnlyCollection<(string Name, string Content)> entries)
    {
        using var stream = File.Create(path);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create);
        foreach (var (name, content) in entries)
        {
            using var entry = archive.CreateEntry(name).Open();
            var bytes = Encoding.UTF8.GetBytes(content);
            entry.Write(bytes);
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory() => Path = Directory.CreateTempSubdirectory("moai-plugin-tests-").FullName;
        public string Path { get; }
        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
