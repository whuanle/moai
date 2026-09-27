using System.Threading;
using System.Threading.Tasks;
using MoAI.AIPlugin.Dynamic.Models;
using MoAI.AIPlugin.Dynamic.Plugins;
using MoAI.Infra.Exceptions;
using Xunit;

namespace MoAI.AIPlugin.Dynamic.Tests;

/// <summary>
/// ClickHouse 只读守卫测试：通用 SQL 白黑名单（写操作/多语句/首关键字，含 SYSTEM/ATTACH 等服务器操作语句）+ ClickHouse 特有的外部数据源表函数黑名单与 FORMAT 子句剥离.
/// </summary>
public class ClickHouseReadOnlyGuardTests
{
    [Theory]
    [InlineData("UPDATE demo SET x = 1")]
    [InlineData("DELETE FROM demo WHERE id = 1")]
    [InlineData("INSERT INTO demo (id) VALUES (1)")]
    [InlineData("CREATE TABLE demo (id Int32) ENGINE = MergeTree")]
    [InlineData("ALTER TABLE demo DELETE WHERE id = 1")]
    [InlineData("DROP TABLE demo")]
    [InlineData("TRUNCATE TABLE demo")]
    [InlineData("SET max_execution_time = 1")]
    [InlineData("GRANT ALL ON demo TO u")]
    [InlineData("SELECT 1; SELECT 2")]
    public void Validate_BaseRules_StillRejected(string sql)
    {
        var violation = ClickHouseReadOnlyGuard.Validate(sql);

        Assert.NotNull(violation);
        Assert.Contains("只允许", violation, System.StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("SELECT 1")]
    [InlineData("SELECT name, value FROM system.columns WHERE database = 'otel' ORDER BY position")]
    [InlineData("SHOW TABLES FROM otel")]
    [InlineData("DESCRIBE otel.otel_traces")]
    [InlineData("WITH x AS (SELECT 1 AS n) SELECT n FROM x")]
    [InlineData("SELECT url, file, format_datetime FROM t WHERE url LIKE '%api%'")]
    [InlineData("SELECT uniq(file), concat(url, 'x') FROM t")]
    public void Validate_ReadOnlySql_Passes(string sql)
    {
        Assert.Null(ClickHouseReadOnlyGuard.Validate(sql));
    }

    [Theory]
    [InlineData("SYSTEM FLUSH LOGS")]
    [InlineData("SYSTEM KILL QUERY WHERE query_id = 'x'")]
    [InlineData("KILL QUERY WHERE query_id = 'x'")]
    [InlineData("ATTACH TABLE demo")]
    [InlineData("EXCHANGE TABLES t1 AND t2")]
    public void Validate_ClickHouseServerOperations_Rejected(string sql)
    {
        var violation = ClickHouseReadOnlyGuard.Validate(sql);

        Assert.Contains("只允许", violation, System.StringComparison.Ordinal);
        Assert.Contains(KeywordLabel(sql), violation, System.StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("SELECT * FROM url('http://evil.example/data.csv', 'CSV')")]
    [InlineData("SELECT * FROM file('access.log', 'LineAsString')")]
    [InlineData("SELECT * FROM s3('https://bucket.s3.amazonaws.com/x', 'CSV')")]
    [InlineData("SELECT * FROM remote('127.0.0.1:9000', db, t)")]
    [InlineData("SELECT * FROM remoteSecure('h:9000', db, t)")]
    [InlineData("SELECT * FROM cluster('c1', db, t)")]
    [InlineData("SELECT * FROM postgresql(127.0.0.1:5432, db, t)")]
    [InlineData("SELECT * FROM mysql('127.0.0.1:3306', db, t)")]
    [InlineData("SELECT * FROM hdfs('hdfs://h/x')")]
    [InlineData("SELECT * FROM iceberg('s3://b/hive')")]
    public void Validate_ExternalSourceTableFunctions_Rejected(string sql)
    {
        var violation = ClickHouseReadOnlyGuard.Validate(sql);

        Assert.Contains("只允许", violation, System.StringComparison.Ordinal);
        Assert.Contains("不允许", violation, System.StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_EmptySql_ReadableError()
    {
        var violation = ClickHouseReadOnlyGuard.Validate("   ");

        Assert.Equal("SQL 不能为空", violation);
    }

    [Fact]
    public void StripTrailingFormatClause_RemovesTrailingFormat()
    {
        Assert.Equal("SELECT 1", ClickHouseReadOnlyGuard.StripTrailingFormatClause("SELECT 1 FORMAT JSON"));
        Assert.Equal("select 1", ClickHouseReadOnlyGuard.StripTrailingFormatClause("select 1 format json ;"));
        Assert.Equal("SELECT 1", ClickHouseReadOnlyGuard.StripTrailingFormatClause("SELECT 1\n  FORMAT JSONEachRow\n"));
        Assert.Equal("SELECT x FROM t", ClickHouseReadOnlyGuard.StripTrailingFormatClause("SELECT x FROM t FORMAT Compact"));
    }

    [Fact]
    public void StripTrailingFormatClause_KeepsQueryWithoutFormat()
    {
        Assert.Equal("SELECT 1", ClickHouseReadOnlyGuard.StripTrailingFormatClause("SELECT 1"));
        Assert.Equal("SELECT format FROM t", ClickHouseReadOnlyGuard.StripTrailingFormatClause("SELECT format FROM t"));
        Assert.Equal("SELECT formatDateTime(Timestamp, 'x')", ClickHouseReadOnlyGuard.StripTrailingFormatClause("SELECT formatDateTime(Timestamp, 'x')"));
        Assert.Equal("SELECT a, format FROM t", ClickHouseReadOnlyGuard.StripTrailingFormatClause("SELECT a, format FROM t"));
    }

    private static string KeywordLabel(string sql)
    {
        var first = sql.Split(' ', System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries)[0];
        return first.ToUpperInvariant();
    }
}
