using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.Storage.Services;
using MoAI.Team.Queries;
using MoAI.Team.Queries.Responses;

namespace MoAI.Team.Handlers;

/// <summary>
/// <inheritdoc cref="QueryTeamUsersCommand"/>
/// </summary>
public class QueryTeamUsersCommandHandler : IRequestHandler<QueryTeamUsersCommand, QueryTeamUsersCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IStorageService _storageService;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryTeamUsersCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="storageService">存储领域服务.</param>
    public QueryTeamUsersCommandHandler(DatabaseContext databaseContext, IStorageService storageService)
    {
        _databaseContext = databaseContext;
        _storageService = storageService;
    }

    /// <inheritdoc/>
    public async Task<QueryTeamUsersCommandResponse> Handle(QueryTeamUsersCommand request, CancellationToken cancellationToken)
    {
        var items = await _databaseContext.TeamUsers
            .Where(x => x.TeamId == request.TeamId)
            .Join(_databaseContext.Users, tu => tu.UserId, u => u.Id, (tu, u) => new { tu.Role, tu.CreateTime, User = u })
            .OrderBy(x => x.User.Id)
            .Select(x => new
            {
                x.User.Id,
                x.User.UserName,
                x.User.NickName,
                x.User.AvatarPath,
                x.User.Email,
                x.User.Phone,
                Role = (int)x.Role,
                x.CreateTime
            })
            .ToListAsync(cancellationToken);

        return new QueryTeamUsersCommandResponse
        {
            Items = items.Select(x => new TeamUserItem
            {
                UserId = x.Id,
                UserName = x.UserName,
                NickName = x.NickName,
                Avatar = string.IsNullOrWhiteSpace(x.AvatarPath)
                    ? string.Empty
                    : _storageService.GetPublicFileUrl(x.AvatarPath).ToString(),
                // 团队成员列表面向团队 Owner/Admin，不外泄完整联系方式：邮箱/手机号在服务端脱敏后返回
                Email = MaskEmail(x.Email),
                Phone = MaskPhone(x.Phone),
                Role = x.Role,
                JoinTime = x.CreateTime
            }).ToList()
        };
    }

    /// <summary>
    /// 邮箱脱敏：本地部分仅保留首字符（如 z***@example.com），无 @ 或本地部分为空返回空串.
    /// </summary>
    private static string MaskEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return string.Empty;
        }

        var at = email.IndexOf('@');
        if (at <= 0 || at == email.Length - 1)
        {
            return string.Empty;
        }

        return $"{email[0]}***{email[at..]}";
    }

    /// <summary>
    /// 手机号脱敏：≥8 位保留前 3 后 4（如 138****5678），其余仅保留首字符，空返回空串.
    /// </summary>
    private static string MaskPhone(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return string.Empty;
        }

        return phone.Length >= 8 ? $"{phone[..3]}****{phone[^4..]}" : $"{phone[0]}****";
    }
}
