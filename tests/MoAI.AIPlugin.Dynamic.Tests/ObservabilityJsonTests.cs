using System.Collections.Generic;
using System.Text.Json;
using Xunit;

namespace MoAI.AIPlugin.Dynamic.Tests;

/// <summary>
/// 观测 JSON 容错展开工具测试.
/// </summary>
public class ObservabilityJsonTests
{
    [Fact]
    public void ToClr_ScalarKinds_Normalized()
    {
        using var document = JsonDocument.Parse("""{"i": 20, "d": 2.5, "s": "文本", "t": true, "f": false, "z": null}""");

        var row = ToRow(document.RootElement);

        Assert.Equal(20L, row["i"]);
        Assert.Equal(2.5, row["d"]);
        Assert.Equal("文本", row["s"]);
        Assert.Equal(true, row["t"]);
        Assert.Equal(false, row["f"]);
        Assert.Null(row["z"]);
    }

    [Fact]
    public void ToClr_NestedAndArrays_Recursed()
    {
        using var document = JsonDocument.Parse("""{"map": {"a": {"b": [1, "two"]}}, "arr": []}""");

        var row = ToRow(document.RootElement);

        var inner = Assert.IsType<Dictionary<string, object?>>(row["map"])["a"];
        var list = Assert.IsType<List<object?>>(Assert.IsType<Dictionary<string, object?>>(inner)["b"]);
        Assert.Equal(1L, Assert.IsType<long>(list[0]));
        Assert.Equal("two", list[1]);
        Assert.Empty(Assert.IsType<List<object?>>(row["arr"]));
    }

    [Fact]
    public void ToClr_NonFiniteNumber_BecomesReadableText()
    {
        using var document = JsonDocument.Parse("""{"big": 1e400}""");

        var row = ToRow(document.RootElement);

        Assert.Equal("INFINITY", row["big"]);
        Assert.IsNotType<double>(row["big"]);
    }

    [Fact]
    public void ToStringMap_ScalarKept_ComplexTruncated()
    {
        using var document = JsonDocument.Parse("""{"__name__": "up", "port": 9100, "job": "node", "tags": ["a", "b"], "nested": {"deep": 1}}""");

        var map = ObservabilityJson.ToStringMap(document.RootElement);

        Assert.Equal(3, map.Count);
        Assert.Equal("up", map["__name__"]);
        Assert.Equal("9100", map["port"]);
        Assert.Equal("node", map["job"]);
        Assert.False(map.ContainsKey("tags"));
        Assert.False(map.ContainsKey("nested"));
    }

    [Fact]
    public void ToStringMap_NonObject_ReturnsEmpty()
    {
        using var document = JsonDocument.Parse("[1,2]");

        Assert.Empty(ObservabilityJson.ToStringMap(document.RootElement));
    }

    [Fact]
    public void ToOtelMap_ArrayAndFlatForms_Normalized()
    {
        using var arrayForm = JsonDocument.Parse("""[{"key":"service.name","value":{"stringValue":"api"}},{"key":"num","value":{"intValue":"5"}},{"key":"ratio","value":{"doubleValue":"0.25"}},{"key":"ok","value":{"boolValue":true}},{"key":"broken"},{"key":"tags","value":{"arrayValue":{"values":[{"stringValue":"x"}]}}}]""");
        var otel = ObservabilityJson.ToOtelMap(arrayForm.RootElement);

        Assert.Equal(5, otel.Count);
        Assert.Equal("api", otel["service.name"]);
        Assert.Equal("5", otel["num"]);
        Assert.Equal("0.25", otel["ratio"]);
        Assert.Equal("true", otel["ok"]);
        Assert.Equal("""{"arrayValue":{"values":[{"stringValue":"x"}]}}""", otel["tags"]);
        Assert.False(otel.ContainsKey("broken"));

        using var flatForm = JsonDocument.Parse("""{"service.name": "web", "port": 8080}""");
        var flat = ObservabilityJson.ToOtelMap(flatForm.RootElement);
        Assert.Equal(2, flat.Count);
        Assert.Equal("web", flat["service.name"]);
        Assert.Equal("8080", flat["port"]);
    }

    [Fact]
    public void Truncate_KeepsMarker()
    {
        var text = new string('x', 300);

        var truncated = ObservabilityJson.Truncate(text, 10);

        Assert.Equal(10, truncated.Length);
        Assert.EndsWith("…", truncated, System.StringComparison.Ordinal);
        Assert.Equal("xxxxxxxxx…", truncated);
        Assert.Equal(text, ObservabilityJson.Truncate(text, 500));
        Assert.Equal(string.Empty, ObservabilityJson.Truncate(text, 0));
    }

    [Fact]
    public void ParseOrThrow_InvalidJson_ThrowsReadable()
    {
        var exception = Assert.Throws<MoAI.Infra.Exceptions.BusinessException>(() => ObservabilityJson.ParseOrThrow("not-json", "Prometheus"));

        Assert.Equal(502, exception.StatusCode);
        Assert.Contains("Prometheus", exception.Message, System.StringComparison.Ordinal);
    }

    private static Dictionary<string, object?> ToRow(JsonElement element)
    {
        var row = new Dictionary<string, object?>(System.StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
        {
            row[property.Name] = ObservabilityJson.ToClr(property.Value);
        }

        return row;
    }
}
