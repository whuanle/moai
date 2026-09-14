using Microsoft.Extensions.DependencyInjection;
using MoAI.Infra.Exceptions;
using MoAI.Settings.Models;
using MoAI.Settings.Services;
using Neo4j.Driver;

namespace MoAI.KnowledgeGraph.Services;

/// <summary>
/// 图数据库驱动提供者：按当前设置缓存并复用 driver，连接信息或方言变化时重建.
/// </summary>
public sealed class GraphDriverProvider : IAsyncDisposable
{
    private const string Neo4jConstraintCypher = "CREATE CONSTRAINT kg_node_id_unique IF NOT EXISTS FOR (n:KgNode) REQUIRE n.id IS UNIQUE";
    private const string Neo4jIndexCypher = "CREATE INDEX kg_node_kg_name IF NOT EXISTS FOR (n:KgNode) ON (n.kgId, n.name)";

    private const string MemgraphNodeIndexCypher = "CREATE INDEX ON :KgNode(id)";
    private const string MemgraphKgIndexCypher = "CREATE INDEX ON :KgNode(kgId)";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly List<IDriver> _retired = new();
    private IDriver? _driver;
    private string? _connectionKey;
    private string? _dialect;
    private volatile bool _initialized;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="GraphDriverProvider"/> class.
    /// </summary>
    /// <param name="scopeFactory">用于在单例中安全解析 scoped 设置服务的作用域工厂.</param>
    public GraphDriverProvider(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    /// <summary>
    /// 获取当前驱动与方言，未开启能力时抛 409.
    /// </summary>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>返回驱动与方言.</returns>
    public async Task<(IDriver Driver, string Dialect)> GetRuntimeAsync(CancellationToken cancellationToken)
    {
        KnowledgeGraphStoreSettings settings;
        using (var scope = _scopeFactory.CreateScope())
        {
            var settingsService = scope.ServiceProvider.GetRequiredService<IKnowledgeGraphSettingsService>();
            settings = await settingsService.GetAsync(cancellationToken);
        }

        if (!settings.Enabled || string.IsNullOrWhiteSpace(settings.Uri))
        {
            throw new BusinessException("未开启知识图谱能力，请先在系统设置中配置图数据库.") { StatusCode = 409 };
        }

        var key = $"{settings.Uri}|{settings.Username}|{settings.Password}|{settings.Dialect}";
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(GraphDriverProvider));
            }

            if (_driver == null || _connectionKey != key)
            {
                IDriver newDriver;
                try
                {
                    newDriver = GraphDatabase.Driver(new Uri(settings.Uri), AuthTokens.Basic(settings.Username, settings.Password));
                }
                catch (Exception ex) when (ex is FormatException or ArgumentException)
                {
                    throw new BusinessException("图数据库连接地址无效，请检查系统设置.") { StatusCode = 409 };
                }

                if (_driver != null)
                {
                    _retired.Add(_driver);
                }

                _driver = newDriver;
                _connectionKey = key;
                _dialect = settings.Dialect;
                _initialized = false;
            }

            return (_driver, _dialect ?? KnowledgeGraphStoreSettings.DialectMemgraph);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// 获取当前驱动，未开启能力时抛 409.
    /// </summary>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>返回 <see cref="IDriver"/>.</returns>
    public async Task<IDriver> GetDriverAsync(CancellationToken cancellationToken)
        => (await GetRuntimeAsync(cancellationToken)).Driver;

    /// <summary>
    /// 幂等初始化索引与约束（每个连接配置一次）；方言决定语句形态.
    /// </summary>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>异步任务.</returns>
    public async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_initialized)
        {
            return;
        }

        var (driver, dialect) = await GetRuntimeAsync(cancellationToken);
        await using var session = driver.AsyncSession();
        if (string.Equals(dialect, KnowledgeGraphStoreSettings.DialectNeo4j, StringComparison.OrdinalIgnoreCase))
        {
            var constraintCursor = await session.RunAsync(Neo4jConstraintCypher);
            await constraintCursor.ConsumeAsync();
            var indexCursor = await session.RunAsync(Neo4jIndexCypher);
            await indexCursor.ConsumeAsync();
        }
        else
        {
            // Memgraph：无 IF NOT EXISTS，重复创建会抛"already exists"客户端异常，按幂等吞掉。
            foreach (var cypher in new[] { MemgraphNodeIndexCypher, MemgraphKgIndexCypher })
            {
                try
                {
                    var cursor = await session.RunAsync(cypher);
                    await cursor.ConsumeAsync();
                }
                catch (Neo4jException ex) when (ex is ClientException && ex.Message.Contains("already exist", StringComparison.OrdinalIgnoreCase))
                {
                }
            }
        }

        _initialized = true;
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _lock.WaitAsync();
        try
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            if (_driver != null)
            {
                await _driver.DisposeAsync();
                _driver = null;
            }

            foreach (var driver in _retired)
            {
                await driver.DisposeAsync();
            }

            _retired.Clear();
        }
        finally
        {
            _lock.Release();
        }
    }
}
