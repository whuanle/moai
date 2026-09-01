using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Account.Queries;
using MoAI.Account.Queries.Responses;
using MoAI.Storage.Services;

namespace MoAI.Account.Queries;

/// <summary>
/// <inheritdoc cref="QueryUserListCommand"/>
/// </summary>
public class QueryUserListCommandHandler : IRequestHandler<QueryUserListCommand, QueryUserListCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IStorageService _storageService;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryUserListCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="storageService"></param>
    public QueryUserListCommandHandler(DatabaseContext databaseContext, IStorageService storageService)
    {
        _databaseContext = databaseContext;
        _storageService = storageService;
    }

    /// <inheritdoc/>
    public async Task<QueryUserListCommandResponse> Handle(QueryUserListCommand request, CancellationToken cancellationToken)
    {
        var rootValue = await _databaseContext.Settings
            .Where(s => s.Key == "root")
            .Select(s => s.Value)
            .FirstOrDefaultAsync(cancellationToken);

        var query = _databaseContext.Users
            .Where(u => u.IsDeleted == 0);

        if (!string.IsNullOrWhiteSpace(request.SearchText))
        {
            var keyword = request.SearchText.Trim();
            query = query.Where(u =>
                u.UserName.Contains(keyword) ||
                u.NickName.Contains(keyword) ||
                u.Email.Contains(keyword));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var users = await query
            .OrderBy(u => u.Id)
            .Skip(request.Skip)
            .Take(request.Take)
            .Select(u => new
            {
                u.Id,
                u.UserName,
                u.NickName,
                u.Email,
                u.Phone,
                u.AvatarPath,
                u.IsAdmin,
                u.IsDisable,
                u.CreateTime
            })
            .ToListAsync(cancellationToken);

        var items = users.Select(u => new QueryUserListCommandResponseItem
        {
            Id = u.Id,
            UserName = u.UserName,
            NickName = u.NickName,
            Email = u.Email,
            Phone = u.Phone,
            Avatar = string.IsNullOrWhiteSpace(u.AvatarPath)
                ? string.Empty
                : _storageService.GetPublicFileUrl(u.AvatarPath).ToString(),
            IsAdmin = u.IsAdmin,
            IsRoot = u.Id.ToString() == rootValue,
            IsDisable = u.IsDisable,
            CreateTime = u.CreateTime
        }).ToList();

        return new QueryUserListCommandResponse
        {
            TotalCount = totalCount,
            Items = items
        };
    }
}
