using System.Diagnostics;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Seed;
using MoAI.Infra.Exceptions;
using MoAI.KnowledgeGraph.Commands;
using MoAI.KnowledgeGraph.Queries.Responses;
using MoAI.Settings.Models;
using Neo4j.Driver;

namespace MoAI.KnowledgeGraph.Handlers;

/// <summary>
/// 测试图数据库连通性：系统设置「知识图谱」开启前的预检入口.
/// 用临时 driver 探活（不复用 GraphDriverProvider 单例，避免未保存的测试凭据污染运行时连接），
/// 请求字段留空回退系统设置已保存值；连接失败返回 ok=false 而非抛 503，便于设置页展示「测试未通过」.
/// </summary>
public class TestKnowledgeGraphConnectionCommandHandler : IRequestHandler<TestKnowledgeGraphConnectionCommand, TestKnowledgeGraphConnectionCommandResponse>
{
    /// <summary>
    /// 临时驱动连接超时：单次建连 5 秒.
    /// </summary>
    private static readonly TimeSpan ConnectionTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// 整体探活限幅：图库不可用时驱动会反复重试且会话 API 不响应取消令牌，以任务竞争裁决.
    /// </summary>
    private static readonly TimeSpan OverallTimeout = TimeSpan.FromSeconds(8);

    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="TestKnowledgeGraphConnectionCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文（读取已保存的连接设置；不经 IKnowledgeGraphSettingsService，因其未开启时短路不回传连接信息）.</param>
    public TestKnowledgeGraphConnectionCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<TestKnowledgeGraphConnectionCommandResponse> Handle(TestKnowledgeGraphConnectionCommand request, CancellationToken cancellationToken)
    {
        var stored = await ResolveStoredConnectionAsync(cancellationToken);

        var uri = Override(request.Uri) ?? stored.Uri;
        var username = Override(request.Username) ?? stored.Username;
        var password = Override(request.Password) ?? stored.Password;
        var dialect = (Override(request.Dialect) ?? stored.Dialect).Trim().ToLowerInvariant();
        if (dialect != KnowledgeGraphStoreSettings.DialectNeo4j)
        {
            dialect = KnowledgeGraphStoreSettings.DialectMemgraph;
        }

        if (string.IsNullOrWhiteSpace(uri))
        {
            throw new BusinessException("请先填写图数据库连接地址.") { StatusCode = 400 };
        }

        IDriver driver;
        try
        {
            driver = GraphDatabase.Driver(new Uri(uri.Trim()), AuthTokens.Basic(username, password), builder => builder.WithConnectionTimeout(ConnectionTimeout));
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException)
        {
            throw new BusinessException("图数据库连接地址无效，请检查填写内容.") { StatusCode = 400 };
        }

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var work = ProbeAsync(driver, dialect, cancellationToken);
            var completed = await Task.WhenAny(work, Task.Delay(OverallTimeout, cancellationToken));
            if (completed != work)
            {
                _ = work.ContinueWith(t => _ = t.Exception, TaskContinuationOptions.OnlyOnFaulted);
                return new TestKnowledgeGraphConnectionCommandResponse { Ok = false, LatencyMs = (long)OverallTimeout.TotalMilliseconds, Dialect = dialect, Error = "连接图数据库超时，请检查服务是否启动." };
            }

            stopwatch.Stop();
            await work;
            return new TestKnowledgeGraphConnectionCommandResponse { Ok = true, LatencyMs = stopwatch.ElapsedMilliseconds, Dialect = dialect };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (BusinessException)
        {
            throw;
        }
        catch (Exception ex) when (ex is ServiceUnavailableException || ex is AuthenticationException)
        {
            return new TestKnowledgeGraphConnectionCommandResponse { Ok = false, LatencyMs = stopwatch.ElapsedMilliseconds, Dialect = dialect, Error = "无法连接图数据库，请检查地址与凭据." };
        }
        catch (Neo4jException ex)
        {
            return new TestKnowledgeGraphConnectionCommandResponse { Ok = false, LatencyMs = stopwatch.ElapsedMilliseconds, Dialect = dialect, Error = $"图数据库响应异常：{ex.Message}" };
        }
        catch (Exception ex)
        {
            return new TestKnowledgeGraphConnectionCommandResponse { Ok = false, LatencyMs = stopwatch.ElapsedMilliseconds, Dialect = dialect, Error = $"测试失败：{ex.Message}" };
        }
        finally
        {
            // Dispose 中断连接池，同时终止被限幅放弃的探活任务；被放弃任务异常已旁路吞掉
            try
            {
                await driver.DisposeAsync();
            }
            catch (Exception ex) when (ex is ObjectDisposedException or Neo4jException)
            {
            }
        }
    }

    /// <summary>
    /// 探活：neo4j 方言 CALL db.labels()，memgraph 方言 RETURN 1（与建接入图探活同口径）.
    /// </summary>
    private static async Task ProbeAsync(IDriver driver, string dialect, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using var session = driver.AsyncSession();
        var cypher = string.Equals(dialect, KnowledgeGraphStoreSettings.DialectNeo4j, StringComparison.OrdinalIgnoreCase)
            ? "CALL db.labels()"
            : "RETURN 1 AS ok";
        var cursor = await session.RunAsync(cypher);
        await cursor.ConsumeAsync();
    }

    /// <summary>
    /// 请求字段非空白则覆盖存储值，否则回退.
    /// </summary>
    private static string? Override(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    /// 读取系统设置中已保存的图数据库连接信息（库值优先，缺失回退注册表默认值）.
    /// </summary>
    private async Task<(string Uri, string Username, string Password, string Dialect)> ResolveStoredConnectionAsync(CancellationToken cancellationToken)
    {
        var keys = new[]
        {
            SettingDefinitions.GraphUriKey,
            SettingDefinitions.GraphUsernameKey,
            SettingDefinitions.GraphPasswordKey,
            SettingDefinitions.GraphDialectKey,
        };

        var values = await _databaseContext.Settings.AsNoTracking()
            .Where(s => keys.Contains(s.Key))
            .ToDictionaryAsync(s => s.Key, s => s.Value, cancellationToken);

        string Resolve(string key)
            => values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
                ? value
                : SettingDefinitions.Find(key)?.DefaultValue ?? string.Empty;

        return (Resolve(SettingDefinitions.GraphUriKey), Resolve(SettingDefinitions.GraphUsernameKey), Resolve(SettingDefinitions.GraphPasswordKey), Resolve(SettingDefinitions.GraphDialectKey));
    }
}
