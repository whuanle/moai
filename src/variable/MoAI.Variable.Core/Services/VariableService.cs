using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Service;
using MoAI.Variable.Services;

namespace MoAI.Variable.Services;

/// <summary>
/// 变量服务实现.
/// </summary>
public partial class VariableService : IVariableService
{
    /// <summary>
    /// <c>${key}</c> 占位符：字母开头，字母/数字/下划线.
    /// </summary>
    [GeneratedRegex(@"\$\{([A-Za-z][A-Za-z0-9_]*)\}")]
    private static partial Regex PlaceholderRegex();

    private readonly DatabaseContext _databaseContext;
    private readonly IAESProvider _aesProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="VariableService"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="aesProvider">AES 加密服务.</param>
    public VariableService(DatabaseContext databaseContext, IAESProvider aesProvider)
    {
        _databaseContext = databaseContext;
        _aesProvider = aesProvider;
    }

    /// <inheritdoc/>
    public async Task<string> SubstituteAsync(long teamId, string content, CancellationToken cancellationToken = default)
    {
        var variables = await _databaseContext.TeamVariables
            .Where(x => x.TeamId == teamId)
            .ToDictionaryAsync(x => x.Key, x => x, StringComparer.Ordinal, cancellationToken);

        return PlaceholderRegex().Replace(content, match =>
        {
            var key = match.Groups[1].Value;
            if (!variables.TryGetValue(key, out var variable))
            {
                // 未匹配到变量的占位符保留原文，便于调用方排查配置缺漏
                return match.Value;
            }

            return variable.IsSecret ? _aesProvider.Decrypt(variable.Value) : variable.Value;
        });
    }
}
