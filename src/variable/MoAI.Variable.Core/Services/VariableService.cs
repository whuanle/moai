using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Models;
using MoAI.Infra.Service;
using MoAI.Variable.Services;

namespace MoAI.Variable.Services;

/// <summary>
/// 变量服务实现.
/// </summary>
public class VariableService : IVariableService
{
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
        var variables = await LoadDecryptedAsync(teamId, cancellationToken);
        return TeamVariableTemplate.Format(content, variables);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<KeyValueString>> SubstituteAsync(long teamId, IReadOnlyCollection<KeyValueString> values, CancellationToken cancellationToken = default)
    {
        if (values.Count == 0)
        {
            return [];
        }

        var variables = await LoadDecryptedAsync(teamId, cancellationToken);
        return values
            .Select(kv => new KeyValueString { Key = kv.Key, Value = TeamVariableTemplate.Format(kv.Value ?? string.Empty, variables) })
            .ToList();
    }

    private async Task<Dictionary<string, string>> LoadDecryptedAsync(long teamId, CancellationToken cancellationToken)
    {
        return await _databaseContext.TeamVariables
            .Where(x => x.TeamId == teamId)
            .ToDictionaryAsync(
                x => x.Key,
                x => x.IsSecret ? _aesProvider.Decrypt(x.Value) : x.Value,
                StringComparer.Ordinal,
                cancellationToken);
    }
}
