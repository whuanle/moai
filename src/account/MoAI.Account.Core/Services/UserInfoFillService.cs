using Microsoft.EntityFrameworkCore;
using MoAI.Account.Services;
using MoAI.Database;
using MoAI.Infra.Models;

namespace MoAI.Account.Services;

/// <summary>
/// 用户信息填充领域服务实现.
/// </summary>
public class UserInfoFillService : IUserInfoFillService
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserInfoFillService"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public UserInfoFillService(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task FillAsync(AuditsInfo item, CancellationToken cancellationToken)
    {
        if (item is null)
        {
            return;
        }

        await FillAsync(new[] { item }, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task FillAsync(IEnumerable<AuditsInfo> items, CancellationToken cancellationToken)
    {
        if (items is null)
        {
            return;
        }

        var list = items.Where(x => x is not null).ToList();
        if (list.Count == 0)
        {
            return;
        }

        var userIds = list
            .Select(x => (long)x.CreateUserId)
            .Concat(list.Select(x => (long)x.UpdateUserId))
            .Distinct()
            .ToArray();

        if (userIds.Length == 0)
        {
            return;
        }

        var userNames = await _databaseContext.Users
            .Where(x => userIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.NickName, cancellationToken);

        foreach (var item in list)
        {
            item.CreateUserName = userNames.TryGetValue((long)item.CreateUserId, out var createUserName)
                ? createUserName
                : string.Empty;
            item.UpdateUserName = userNames.TryGetValue((long)item.UpdateUserId, out var updateUserName)
                ? updateUserName
                : string.Empty;
        }
    }
}
