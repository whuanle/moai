using System.Threading.Tasks;
using Maomi;
using Microsoft.Extensions.Logging;

namespace MoAI.AI.Services;

/// <summary>
/// 沙箱孤儿回收任务：清理本系统创建但对应会话已不存在的沙箱（沙箱自身 TTL 之外的第二道防线）.
/// </summary>
[InjectOnScoped]
public sealed class SandboxReaperJob
{
    private readonly IAppSandboxService _sandboxService;
    private readonly ILogger<SandboxReaperJob> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SandboxReaperJob"/> class.
    /// </summary>
    /// <param name="sandboxService">会话沙箱服务.</param>
    /// <param name="logger">日志.</param>
    public SandboxReaperJob(IAppSandboxService sandboxService, ILogger<SandboxReaperJob> logger)
    {
        _sandboxService = sandboxService;
        _logger = logger;
    }

    /// <summary>
    /// 执行回收.
    /// </summary>
    /// <returns>异步任务.</returns>
    public async Task InvokeAsync()
    {
        try
        {
            var killed = await _sandboxService.ReapOrphansAsync().ConfigureAwait(false);
            if (killed > 0)
            {
                _logger.LogInformation("沙箱回收任务清理了 {Count} 个孤儿沙箱.", killed);
            }
        }
#pragma warning disable CA1031 // 定时任务失败不抛出，避免影响后续调度
        catch (System.Exception ex)
        {
            _logger.LogError(ex, "沙箱回收任务执行失败.");
        }
#pragma warning restore CA1031
    }
}
