using System.Text.Json;
using Xunit;

namespace MoAI.AIPlugin.Dynamic.Tests;

/// <summary>
/// Grafana 响应解析测试.
/// </summary>
public class GrafanaResponseParserTests
{
    [Fact]
    public void EpochMsToIso_UnixMilliseconds_FormatsUtcIso()
    {
        Assert.Equal("2026-09-29T00:00:00Z", GrafanaResponseParser.EpochMsToIso("1790640000000"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("0")]
    [InlineData("-5")]
    public void EpochMsToIso_Invalid_ReturnsEmpty(string epochMs)
    {
        Assert.Equal(string.Empty, GrafanaResponseParser.EpochMsToIso(epochMs));
    }

    [Fact]
    public void ParseAnnotations_FullShape_ParsesFields()
    {
        using var document = JsonDocument.Parse(
            """[{"id":7,"text":"deploy nginx v2","tags":["deploy","alerting"],"time":1790640000000,"timeEnd":1790640060000,"dashboardId":12,"panelId":3}]""");

        var annotations = GrafanaResponseParser.ParseAnnotations(document.RootElement);

        var annotation = Assert.Single(annotations);
        Assert.Equal(7, annotation.Id);
        Assert.Equal("deploy nginx v2", annotation.Text);
        Assert.Equal(2, annotation.Tags.Count);
        Assert.Contains("alerting", annotation.Tags);
        Assert.Equal("2026-09-29T00:00:00Z", annotation.TimeFrom);
        Assert.Equal("2026-09-29T00:01:00Z", annotation.TimeTo);
        Assert.Equal(12, annotation.DashboardId);
        Assert.Equal(3, annotation.PanelId);
    }

    [Fact]
    public void ParseDashboards_ParsesFields()
    {
        using var document = JsonDocument.Parse(
            """[{"id":12,"uid":"ngi-abc","title":"Nginx 概览","url":"/d/ngi-abc/nginx","type":"dash-db"}]""");

        var dashboards = GrafanaResponseParser.ParseDashboards(document.RootElement);

        var dashboard = Assert.Single(dashboards);
        Assert.Equal(12, dashboard.Id);
        Assert.Equal("ngi-abc", dashboard.Uid);
        Assert.Equal("Nginx 概览", dashboard.Title);
        Assert.Equal("dash-db", dashboard.Type);
    }

    [Fact]
    public void ParseHealth_ObjectToMap()
    {
        using var document = JsonDocument.Parse("""{"commit":"abc123","database":"ok","version":"11.2.0"}""");

        var health = GrafanaResponseParser.ParseHealth(document.RootElement);

        Assert.Equal(3, health.Count);
        Assert.Equal("ok", health["database"]);
        Assert.Equal("11.2.0", health["version"]);
    }
}
