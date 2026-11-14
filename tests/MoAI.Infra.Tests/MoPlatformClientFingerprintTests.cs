using System;
using System.IO;
using MoAI.Infra;
using Xunit;

namespace MoAI.Infra.Tests;

public class MoPlatformClientFingerprintTests
{
    [Fact]
    public void LoadOrCreate_ShouldPersistAndReuseFingerprint()
    {
        var directory = Path.Combine(Path.GetTempPath(), "moai-fingerprint-tests", Guid.CreateVersion7().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "fingerprint");

        try
        {
            var first = MoPlatformClientFingerprint.LoadOrCreate(path);
            var second = MoPlatformClientFingerprint.LoadOrCreate(path);

            Assert.Matches("^[A-F0-9]{64}$", first);
            Assert.Equal(first, second);
            Assert.Equal(first, File.ReadAllText(path));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void LoadOrCreate_ShouldReplaceMalformedFingerprint()
    {
        var directory = Path.Combine(Path.GetTempPath(), "moai-fingerprint-tests", Guid.CreateVersion7().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "fingerprint");
        File.WriteAllText(path, "not-a-fingerprint");

        try
        {
            var fingerprint = MoPlatformClientFingerprint.LoadOrCreate(path);

            Assert.Matches("^[A-F0-9]{64}$", fingerprint);
            Assert.Equal(fingerprint, File.ReadAllText(path));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
