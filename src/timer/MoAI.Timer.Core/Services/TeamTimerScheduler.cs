using System;
using System.Text.RegularExpressions;
using Maomi;
using MoAI.Hangfire.Services;
using MoAI.Infra.Exceptions;
using MoAI.Timer.Jobs;

namespace MoAI.Timer.Services;

/// <summary>
/// 团队定时器调度管理：每个定时器对应一个 Hangfire 周期任务（key <c>team-timer:{timerId}</c>），
/// cron 为 UTC 时区 5 段标准表达式（与知识库外部源定时爬取口径一致）.
/// </summary>
[InjectOnScoped]
public partial class TeamTimerScheduler
{
    private const string KeyPrefix = "team-timer:";

    private readonly IRecurringJobService _recurringJobService;

    /// <summary>
    /// Initializes a new instance of the <see cref="TeamTimerScheduler"/> class.
    /// </summary>
    /// <param name="recurringJobService">定时任务服务.</param>
    public TeamTimerScheduler(IRecurringJobService recurringJobService)
    {
        _recurringJobService = recurringJobService;
    }

    /// <summary>
    /// 定时器对应的周期任务 key.
    /// </summary>
    /// <param name="timerId">定时器 id.</param>
    /// <returns>周期任务 key.</returns>
    public static string GetKey(Guid timerId) => KeyPrefix + timerId.ToString("N");

    /// <summary>
    /// 注册/更新定时器的周期任务.
    /// </summary>
    /// <param name="timerId">定时器 id.</param>
    /// <param name="cron">cron 表达式（UTC）.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task ScheduleAsync(Guid timerId, string cron)
    {
        ValidateCron(cron);
        await _recurringJobService.AddOrUpdateRecurringJobAsync<TeamTimerRunJobCommand, TeamTimerJobParams>(
            GetKey(timerId), cron, new TeamTimerJobParams { TimerId = timerId });
    }

    /// <summary>
    /// 移除定时器的周期任务.
    /// </summary>
    /// <param name="timerId">定时器 id.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task UnscheduleAsync(Guid timerId)
    {
        await _recurringJobService.RemoveRecurringJobAsync(GetKey(timerId));
    }

    /// <summary>
    /// 查询下次调度时间.
    /// </summary>
    /// <param name="timerId">定时器 id.</param>
    /// <returns>下次调度时间，未启用调度为 null.</returns>
    public async Task<DateTimeOffset?> QueryNextRunTimeAsync(Guid timerId)
    {
        var job = await _recurringJobService.QueryJobAsync(GetKey(timerId));
        return job.IsExist ? job.NextExecution : null;
    }

    /// <summary>
    /// 每个字段允许：*、数字区间（a-b）、步进（/n）、列表（逗号分隔）.
    /// </summary>
    /// <returns>字段正则.</returns>
    [GeneratedRegex(@"^(\*|\d+|\d+-\d+)(/\d+)?(\,(\*|\d+|\d+-\d+)(/\d+)?)*$")]
    private static partial Regex FieldRegex();

    /// <summary>
    /// 校验 cron 表达式：5 段标准格式，字段仅允许 *、数字、区间（a-b）、步进（/n）与列表；
    /// 必须在落库前调用，避免非法表达式落库后注册调度失败.
    /// </summary>
    /// <param name="cron">cron 表达式.</param>
    public static void ValidateCron(string cron) => EnsureCronValid(cron);

    /// <summary>
    /// 校验 cron 表达式.
    /// </summary>
    /// <param name="cron">cron 表达式.</param>
    private static void EnsureCronValid(string cron)
    {
        var fields = cron.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (fields.Length != 5)
        {
            throw new BusinessException("cron 表达式必须为 5 段标准格式（分 时 日 月 周）.") { StatusCode = 400 };
        }

        int[][] ranges =
        [
            [0, 59], [0, 23], [1, 31], [1, 12], [0, 7],
        ];

        for (var i = 0; i < fields.Length; i++)
        {
            if (fields[i] == "*")
            {
                continue;
            }

            foreach (var part in fields[i].Split(','))
            {
                if (!FieldRegex().IsMatch(part))
                {
                    throw new BusinessException($"cron 表达式第 {i + 1} 段（{fields[i]}）不合法.") { StatusCode = 400 };
                }

                foreach (var match in Regex.Matches(part, @"\d+").Cast<Match>())
                {
                    var num = int.Parse(match.Value);
                    if (num < ranges[i][0] || num > ranges[i][1])
                    {
                        throw new BusinessException($"cron 表达式第 {i + 1} 段（{fields[i]}）取值超出范围 {ranges[i][0]}-{ranges[i][1]}.") { StatusCode = 400 };
                    }
                }
            }
        }
    }
}
