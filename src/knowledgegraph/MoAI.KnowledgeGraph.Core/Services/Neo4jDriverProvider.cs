using Neo4j.Driver;
using MoAI.Infra.Exceptions;
using MoAI.Settings.Services;

namespace MoAI.KnowledgeGraph.Services;

/// <summary>
/// Neo4j 驱动提供者：按当前设置缓存并复用 driver，连接信息变化时重建.
/// </summary>
public sealed class Neo4jDriverProvider : IAsyncDisposable
{
    private const string ConstraintCypher = "CREATE CONSTRAINT kg_node_id_unique IF NOT EXISTS FOR (n:KgNode) REQUIRE n.id IS UNIQUE";
    private const string IndexCypher = "CREATE INDEX kg_node_kg_name IF NOT EXISTS FOR (n:KgNode) ON (n.kgId, n.name)";

    private readonly IKnowledgeGraphSettingsService _settingsService;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly List<IDriver> _retired = new();
    private IDriver? _driver;
    private string? _connectionKey;
    private volatile bool _initialized;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="Neo4jDriverProvider"/> class.
    /// </summary>
    /// <param name="settingsService">知识图谱设置读取服务.</param>
    public Neo4jDriverProvider(IKnowledgeGraphSettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    /// <summary>
    /// 获取当前驱动，未开启能力时抛 409.
    /// </summary>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>返回 <see cref="IDriver"/>.</returns>
    public async Task<IDriver> GetDriverAsync(CancellationToken cancellationToken)
    {
        var settings = await _settingsService.GetAsync(cancellationToken);
        if (!settings.Enabled || string.IsNullOrWhiteSpace(settings.Uri))
        {
            throw new BusinessException("未开启知识图谱能力，请先在系统设置中配置 Neo4j.") { StatusCode = 409 };
        }

        var key = $"{settings.Uri}|{settings.Username}|{settings.Password}";
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(Neo4jDriverProvider));
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
                    throw new BusinessException("Neo4j 连接地址无效，请检查系统设置.") { StatusCode = 409 };
                }

                if (_driver != null)
                {
                    _retired.Add(_driver);
                }

                _driver = newDriver;
                _connectionKey = key;
                _initialized = false;
            }

            return _driver;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// 幂等初始化约束与索引（每个进程一次）.
    /// </summary>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>异步任务.</returns>
    public async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_initialized)
        {
            return;
        }

        var driver = await GetDriverAsync(cancellationToken);
        await using var session = driver.AsyncSession();
        var constraintCursor = await session.RunAsync(ConstraintCypher);
        await constraintCursor.ConsumeAsync();
        var indexCursor = await session.RunAsync(IndexCypher);
        await indexCursor.ConsumeAsync();
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
