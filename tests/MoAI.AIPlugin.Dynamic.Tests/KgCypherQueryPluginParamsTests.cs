using System.Collections.Generic;
using System.Text.Json;
using MoAI.AIPlugin.Dynamic.Models;
using MoAI.AIPlugin.Dynamic.Plugins;
using MoAI.Infra.Exceptions;
using Xunit;

namespace MoAI.AIPlugin.Dynamic.Tests;

/// <summary>
/// <see cref="KgCypherQueryPlugin.NormalizeParams"/> 参数归一行为测试：钉死整数归一为 long（不得装箱为 Double）、数组/对象抛教学错误.
/// </summary>
public class KgCypherQueryPluginParamsTests
{
    [Fact]
    public void NormalizeParams_IntegerParam_BoxesAsLong_NotDouble()
    {
        var result = KgCypherQueryPlugin.NormalizeParams(DeserializeParams("""{"n": 20}"""))!;

        var n = Assert.IsType<long>(result["n"]);
        Assert.Equal(20L, n);
    }

    [Fact]
    public void NormalizeParams_DecimalParam_BoxesAsDouble()
    {
        var result = KgCypherQueryPlugin.NormalizeParams(DeserializeParams("""{"x": 2.5}"""))!;

        var x = Assert.IsType<double>(result["x"]);
        Assert.Equal(2.5, x);
    }

    [Fact]
    public void NormalizeParams_ScalarKinds_NormalizedCorrectly()
    {
        var result = KgCypherQueryPlugin.NormalizeParams(DeserializeParams("""{"s": "仓库", "t": true, "f": false, "z": null}"""))!;

        Assert.Equal("仓库", Assert.IsType<string>(result["s"]));
        Assert.True(Assert.IsType<bool>(result["t"]));
        Assert.False(Assert.IsType<bool>(result["f"]));
        Assert.Null(result["z"]);
    }

    [Fact]
    public void NormalizeParams_ArrayValue_ThrowsTeachingError()
    {
        var ex = Assert.Throws<BusinessException>(() =>
            KgCypherQueryPlugin.NormalizeParams(DeserializeParams("""{"arr": [1, 2]}""")));

        Assert.Equal(400, ex.StatusCode);
        Assert.Contains("查询参数仅支持字符串/数字/布尔", ex.Message);
        Assert.Contains("arr", ex.Message);
    }

    [Fact]
    public void NormalizeParams_ObjectValue_ThrowsTeachingError()
    {
        var ex = Assert.Throws<BusinessException>(() =>
            KgCypherQueryPlugin.NormalizeParams(DeserializeParams("""{"k": {"a": 1}}""")));

        Assert.Equal(400, ex.StatusCode);
        Assert.Contains("查询参数仅支持字符串/数字/布尔", ex.Message);
        Assert.Contains("k", ex.Message);
    }

    [Fact]
    public void NormalizeParams_NullOrEmpty_ReturnsNull()
    {
        Assert.Null(KgCypherQueryPlugin.NormalizeParams(null));
        Assert.Null(KgCypherQueryPlugin.NormalizeParams(new Dictionary<string, object?>()));
    }

    [Fact]
    public void NormalizeParams_AlreadyBoxedClrValues_PassThrough()
    {
        var raw = new Dictionary<string, object?> { ["n"] = 20L, ["s"] = "kw" };

        var result = KgCypherQueryPlugin.NormalizeParams(raw)!;

        Assert.Equal(20L, result["n"]);
        Assert.Equal("kw", result["s"]);
    }

    /// <summary>
    /// 按生产路径（PluginExecutor 用 STJ 反序列化请求）构造 Params：object? 值落到 JsonElement.
    /// </summary>
    private static Dictionary<string, object?>? DeserializeParams(string paramsJson)
    {
        var request = JsonSerializer.Deserialize<KgCypherQueryRequest>($"{{ \"Params\": {paramsJson} }}");

        return request!.Params;
    }
}
