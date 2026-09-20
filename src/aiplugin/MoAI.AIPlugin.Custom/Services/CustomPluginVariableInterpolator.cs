using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Maomi;
using MoAI.Database.Entities;
using MoAI.Infra.Extensions;
using MoAI.Infra.Models;
using MoAI.Variable.Services;

namespace MoAI.AIPlugin.Services;

/// <summary>
/// 自定义插件团队变量插值器：把插件 Header/Query 值中的 <c>{key}</c> 占位符替换为插件所属团队的变量值（私密变量解密）.
/// <para>仅团队插件（teamId &gt; 0）参与插值；插值结果仅在服务端内存中使用，数据库中始终保存原始占位符.</para>
/// </summary>
[InjectOnScoped]
public sealed class CustomPluginVariableInterpolator
{
    private readonly IVariableService _variableService;

    /// <summary>
    /// Initializes a new instance of the <see cref="CustomPluginVariableInterpolator"/> class.
    /// </summary>
    /// <param name="variableService">团队变量服务.</param>
    public CustomPluginVariableInterpolator(IVariableService variableService)
    {
        _variableService = variableService;
    }

    /// <summary>
    /// 返回插值后的插件副本；无团队归属或 Header/Query 无需插值时原样返回.
    /// </summary>
    /// <param name="custom">自定义插件实体（数据库跟踪实体，不会被修改）.</param>
    /// <param name="teamId">插件所属团队 id，0 表示系统/管理员插件.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>Header/Query 已插值的插件副本.</returns>
    public async Task<PluginCustomEntity> InterpolateAsync(PluginCustomEntity custom, int teamId, CancellationToken cancellationToken = default)
    {
        if (teamId <= 0 || (string.IsNullOrEmpty(custom.Headers) && string.IsNullOrEmpty(custom.Queries)))
        {
            return custom;
        }

        var headers = DeserializeKeyValues(custom.Headers);
        var queries = DeserializeKeyValues(custom.Queries);
        if (headers.Count == 0 && queries.Count == 0)
        {
            return custom;
        }

        // headers + queries 合并一次插值，只查一次团队变量
        var combined = headers.Concat(queries).ToList();
        var interpolated = await _variableService.SubstituteAsync(teamId, combined, cancellationToken);

        return new PluginCustomEntity
        {
            Id = custom.Id,
            Server = custom.Server,
            Headers = interpolated.Take(headers.Count).ToList().ToJsonString(),
            Queries = interpolated.Skip(headers.Count).ToList().ToJsonString(),
            Type = custom.Type,
            OpenapiFileId = custom.OpenapiFileId,
            OpenapiFileName = custom.OpenapiFileName,
        };
    }

    private static List<KeyValueString> DeserializeKeyValues(string? json)
        => json.JsonToObject<List<KeyValueString>>() ?? [];
}
