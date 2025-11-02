using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Common.Queries.Response;
using MoAI.Database;
using MoAI.User.Queries;

namespace MoAI.User.Handlers;

/// <summary>
/// 填充审计属性信息.
/// </summary>
public class FillUserInfoCommandHandler : IRequestHandler<FillUserInfoCommand, FillUserInfoCommandResponse>
{
    private readonly DatabaseContext _dbContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="FillUserInfoCommandHandler"/> class.
    /// </summary>
    /// <param name="dbContext"></param>
    public FillUserInfoCommandHandler(DatabaseContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc/>
    public async Task<FillUserInfoCommandResponse> Handle(FillUserInfoCommand request, CancellationToken cancellationToken)
    {
        var createUserIds = request.Items.Select(x => x.CreateUserId).ToArray();
        var updateUserIds = request.Items.Select(x => x.UpdateUserId).ToArray();
        var userIds = new HashSet<int>();
        userIds.UnionWith(createUserIds);
        userIds.UnionWith(updateUserIds);

        if (userIds.Count == 0)
        {
            return new FillUserInfoCommandResponse { Items = request.Items };
        }

        var userNames = await _dbContext.Users.Where(x => userIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.NickName);

        foreach (var item in request.Items)
        {
            if (userNames.TryGetValue(item.CreateUserId, out var createUserName))
            {
                item.CreateUserName = createUserName;
            }
            else
            {
                item.CreateUserName = string.Empty;
            }

            if (userNames.TryGetValue(item.UpdateUserId, out var updateUserName))
            {
                item.UpdateUserName = updateUserName;
            }
            else
            {
                item.UpdateUserName = string.Empty;
            }
        }

        return new FillUserInfoCommandResponse { Items = request.Items };
    }
}
