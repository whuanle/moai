using MoAI.Settings.Models;
using Xunit;

namespace MoAI.App.Tests;

/// <summary>
/// K8s 数量格式（沙箱 CPU / 内存限制）解析.
/// </summary>
public class SandboxQuantityTests
{
    [Theory]
    [InlineData("1", 1000)]
    [InlineData("4", 4000)]
    [InlineData("0.5", 500)]
    [InlineData("500m", 500)]
    [InlineData("2000m", 2000)]
    [InlineData(" 1 ", 1000)]
    public void TryParseCpu_Valid_ReturnsMillicores(string value, long expected)
    {
        var ok = SandboxQuantity.TryParseCpu(value, out var millicores);

        Assert.True(ok);
        Assert.Equal(expected, millicores);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("abc")]
    [InlineData("1x")]
    [InlineData("1核心")]
    [InlineData("-1")]
    [InlineData("m")]
    public void TryParseCpu_Invalid_ReturnsFalse(string value)
    {
        Assert.False(SandboxQuantity.TryParseCpu(value, out _));
    }

    [Theory]
    [InlineData("512Mi", 512L * 1024 * 1024)]
    [InlineData("2Gi", 2L * 1024 * 1024 * 1024)]
    [InlineData("1Ki", 1024)]
    [InlineData("1024", 1024)]
    [InlineData("1G", 1_000_000_000L)]
    [InlineData("1.5Gi", 1610612736L)]
    [InlineData(" 2Gi ", 2L * 1024 * 1024 * 1024)]
    public void TryParseMemory_Valid_ReturnsBytes(string value, long expected)
    {
        var ok = SandboxQuantity.TryParseMemory(value, out var bytes);

        Assert.True(ok);
        Assert.Equal(expected, bytes);
    }

    [Theory]
    [InlineData("")]
    [InlineData("2gi")]
    [InlineData("2GB")]
    [InlineData("abc")]
    [InlineData("-1Mi")]
    [InlineData("Mi")]
    public void TryParseMemory_Invalid_ReturnsFalse(string value)
    {
        Assert.False(SandboxQuantity.TryParseMemory(value, out _));
    }
}
