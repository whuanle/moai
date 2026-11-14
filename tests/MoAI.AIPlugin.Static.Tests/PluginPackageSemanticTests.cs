using System.IO.Compression;
using System.Text;
using MoAI.AIPlugin.Services;
using MoAI.Infra.Exceptions;
using Xunit;

namespace MoAI.AIPlugin.Static.Tests;

public class PluginPackageSemanticTests
{
    [Fact]
    public async Task ExtractAsync_ReadsSemanticBundle()
    {
        var root = Path.Combine(Path.GetTempPath(), "moai-semantic-tests", Guid.CreateVersion7().ToString("N"));
        Directory.CreateDirectory(root);
        var archivePath = Path.Combine(root, "package.zip");
        var contentPath = Path.Combine(root, "content");
        try
        {
            CreatePackage(archivePath, SemanticJson("always"));

            var result = await PluginPackageArchive.ExtractAsync(archivePath, contentPath, PackageManifest(), "semantic_demo", CancellationToken.None);

            Assert.NotNull(result.Semantic);
            Assert.Equal(1, result.Semantic!.FormatVersion);
            Assert.Equal("数据库", result.Category);
            Assert.Contains("database", result.Semantic.UsageNotes, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("Call boundaries", Assert.Single(result.Semantic.Notes).Title);
            Assert.Equal("run", Assert.Single(result.Semantic.ToolMetas).ToolKey);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ExtractAsync_RejectsInvalidSemanticMode()
    {
        var root = Path.Combine(Path.GetTempPath(), "moai-semantic-tests", Guid.CreateVersion7().ToString("N"));
        Directory.CreateDirectory(root);
        var archivePath = Path.Combine(root, "package.zip");
        var contentPath = Path.Combine(root, "content");
        try
        {
            CreatePackage(archivePath, SemanticJson("hidden"));

            var exception = await Assert.ThrowsAsync<BusinessException>(() => PluginPackageArchive.ExtractAsync(
                archivePath, contentPath, PackageManifest(), "semantic_demo", CancellationToken.None));

            Assert.Equal(400, exception.StatusCode);
            Assert.Contains("semantic.json", exception.Message);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static MoPlatformPluginManifest PackageManifest() => new()
    {
        PluginId = "1",
        Key = "semantic_demo",
        VersionId = "2",
        Version = "1.0.0",
        SchemaVersion = 1,
        ManifestKey = "semantic_demo",
        TargetFramework = "net10.0",
        EntryAssemblyPath = "semantic_demo.dll",
    };

    private static string SemanticJson(string mode) => $$"""
        {
          "formatVersion": 1,
          "usageNotes": "Use read-only database queries.",
          "notes": [
            { "toolKey": "", "title": "Call boundaries", "content": "Only query user-selected data.", "mode": "{{mode}}", "sortOrder": 0 }
          ],
          "toolMetas": [
            { "toolKey": "run", "extraNotes": "Read-only.", "callPolicy": "", "readOnlyHint": true, "destructiveHint": false }
          ]
        }
        """;

    private static void CreatePackage(string archivePath, string semanticJson)
    {
        using var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create);
        WriteEntry(archive, "semantic_demo.dll", "not loaded by archive extraction");
        WriteEntry(archive, "plugin.json", """{"key":"semantic_demo","version":"1.0.0","name":"Semantic Demo","category":"数据库"}""");
        WriteEntry(archive, "semantic.json", semanticJson);
    }

    private static void WriteEntry(ZipArchive archive, string name, string content)
    {
        using var writer = new StreamWriter(archive.CreateEntry(name).Open(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(content);
    }
}
