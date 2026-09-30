using System.Text.Json;
using Xunit;

namespace MoAI.AIPlugin.Dynamic.Tests;

/// <summary>
/// ClickStack 响应解析测试.
/// </summary>
public class ClickStackResponseParserTests
{
    [Fact]
    public void ParseSources_LogMetricSession_ParsesFieldsWithMetricTableFallback()
    {
        using var document = JsonDocument.Parse("""
            {
              "data": [
                {
                  "id": "src-log-1",
                  "name": "应用日志",
                  "kind": "log",
                  "from": { "databaseName": "hyperdx", "tableName": "otel_logs" },
                  "defaultTableSelectExpression": "Timestamp,ServiceName,Body"
                },
                {
                  "id": "src-metric-1",
                  "name": "主机指标",
                  "kind": "metric",
                  "from": { "databaseName": "hyperdx", "tableName": null },
                  "metricTables": { "sum": "otel_metrics_sum", "gauge": "otel_metrics_gauge", "exp_histogram": "otel_metrics_exp_histogram" }
                },
                {
                  "id": "src-session-1",
                  "name": "会话",
                  "kind": "session",
                  "from": { "databaseName": "hyperdx", "tableName": "hyperdx_sessions" },
                  "disabled": true
                }
              ]
            }
            """);

        var sources = ClickStackResponseParser.ParseSources(document.RootElement);

        Assert.Equal(3, sources.Count);
        Assert.Equal("src-log-1", sources[0].Id);
        Assert.Equal("log", sources[0].Kind);
        Assert.Equal("hyperdx", sources[0].Database);
        Assert.Equal("otel_logs", sources[0].Table);
        Assert.Equal("Timestamp,ServiceName,Body", sources[0].DefaultSelect);
        Assert.False(sources[0].Disabled);
        Assert.Equal("otel_metrics_sum", sources[1].Table);
        Assert.False(sources[1].Disabled);
        Assert.Equal("session", sources[2].Kind);
        Assert.True(sources[2].Disabled);
    }

    [Fact]
    public void ParseSources_MissingOrMalformedData_ReturnsEmpty()
    {
        using var document = JsonDocument.Parse("{}");

        Assert.Empty(ClickStackResponseParser.ParseSources(document.RootElement));
    }

    [Fact]
    public void ParseSearchRows_MixedValueKinds_NormalizesToClr()
    {
        using var document = JsonDocument.Parse("""
            {
              "data": [
                { "Timestamp": "2026-09-29T01:00:00Z", "SeverityText": "ERROR", "Duration": 12, "Ratio": 0.5, "Ok": true, "Extra": null },
                { "Timestamp": "2026-09-29T01:01:00Z", "Body": { "raw": "nest" }, "Tags": ["a", "b"] }
              ]
            }
            """);

        var rows = ClickStackResponseParser.ParseSearchRows(document.RootElement);

        Assert.Equal(2, rows.Count);
        Assert.Equal("ERROR", rows[0]["SeverityText"]);
        Assert.Equal(12L, rows[0]["Duration"]);
        Assert.Equal(0.5, rows[0]["Ratio"]);
        Assert.Equal(true, rows[0]["Ok"]);
        Assert.Null(rows[0]["Extra"]);
        Assert.Equal("nest", Assert.IsType<Dictionary<string, object?>>(rows[1]["Body"])["raw"]);
        Assert.IsType<List<object?>>(rows[1]["Tags"]);
    }

    [Fact]
    public void ParseSearchRows_MissingData_ReturnsEmpty()
    {
        using var document = JsonDocument.Parse("""{"rows": 0}""");

        Assert.Empty(ClickStackResponseParser.ParseSearchRows(document.RootElement));
    }

    [Fact]
    public void GetSearchRowCount_UsesRowsField_FallsBackToCallerValue()
    {
        using var withRows = JsonDocument.Parse("""{"data": [{}, {}], "rows": 57}""");
        using var withoutRows = JsonDocument.Parse("""{"data": [{}, {}, {}]}""");

        Assert.Equal(57, ClickStackResponseParser.GetSearchRowCount(withRows.RootElement, 2));
        Assert.Equal(3, ClickStackResponseParser.GetSearchRowCount(withoutRows.RootElement, 3));
    }

    [Fact]
    public void ParseChartPoints_ParsesBucketValueAndGroup()
    {
        using var document = JsonDocument.Parse("""
            {
              "data": [
                { "ts_bucket": 1735689600000, "series_0.data": 12.5, "group": ["moai", "ERROR"] },
                { "ts_bucket": 1735689900000, "series_0.data": 0, "group": ["moai", "INFO"] }
              ]
            }
            """);

        var points = ClickStackResponseParser.ParseChartPoints(document.RootElement);

        Assert.Equal(2, points.Count);
        Assert.Equal("2025-01-01T00:00:00.000Z", points[0].TsBucket);
        Assert.Equal(12.5, points[0].Value);
        Assert.Equal(new[] { "moai", "ERROR" }, points[0].Group);
        Assert.Equal(0, points[1].Value);
    }

    [Fact]
    public void ParseChartPoints_MissingValueOrData_ReturnsNullOrEmpty()
    {
        using var document = JsonDocument.Parse("""{"data": [{ "ts_bucket": 1735689600000 }]}""");

        var points = ClickStackResponseParser.ParseChartPoints(document.RootElement);

        Assert.Single(points);
        Assert.Null(points[0].Value);
        Assert.Empty(points[0].Group);

        using var empty = JsonDocument.Parse("{}");
        Assert.Empty(ClickStackResponseParser.ParseChartPoints(empty.RootElement));
    }

    [Fact]
    public void EpochMsToIso_FormatsMillisAndRejectsNonPositive()
    {
        Assert.Equal("2025-01-01T00:00:00.000Z", ClickStackResponseParser.EpochMsToIso(1735689600000));
        Assert.Equal(string.Empty, ClickStackResponseParser.EpochMsToIso(0));
        Assert.Equal(string.Empty, ClickStackResponseParser.EpochMsToIso(-1));
    }

    [Theory]
    [InlineData("{\"message\": \"source not found\"}", "source not found")]
    [InlineData("{\"error\": \"invalid granularity\"}", "invalid granularity")]
    [InlineData("{\"error\": {\"message\": \"bad series\"}}", "bad series")]
    public void ExtractErrorMessage_SupportsMessageAndErrorForms(string json, string expected)
    {
        using var document = JsonDocument.Parse(json);

        Assert.Equal(expected, ClickStackResponseParser.ExtractErrorMessage(document.RootElement));
    }

    [Fact]
    public void ExtractErrorMessage_NoError_ReturnsNull()
    {
        using var document = JsonDocument.Parse("""{"data": []}""");

        Assert.Null(ClickStackResponseParser.ExtractErrorMessage(document.RootElement));
    }

    [Theory]
    [InlineData("2026-09-29T00:00:00Z", 0, 0)]
    [InlineData("2026-09-29 08:00:00", 8, 0)]
    [InlineData("2026-09-29T08:30:00+08:00", 0, 30)]
    public void TryParseTime_ParsesCommonForms_AsUtc(string input, int utcHour, int utcMinute)
    {
        Assert.True(ClickStackResponseParser.TryParseTime(input, out var time));
        Assert.Equal(2026, time.Year);
        Assert.Equal(9, time.Month);
        Assert.Equal(29, time.Day);
        Assert.Equal(utcHour, time.Hour);
        Assert.Equal(utcMinute, time.Minute);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-time")]
    public void TryParseTime_Invalid_ReturnsFalse(string input)
    {
        Assert.False(ClickStackResponseParser.TryParseTime(input, out _));
    }
}
