using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MoAI.AIPlugin.Contracts;
using MoAI.AIPlugin.Models;
using MoAI.Database;

namespace MoAI.AIPlugin.Services;

/// <summary>
/// 插件执行上下文访问器默认实现：由执行引擎在插件作用域内注入上下文，
/// 用户详细信息按需从用户表懒加载并在本次执行内缓存.
/// </summary>
public class PluginRunContextAccessor : IPluginRunContextAccessor
{
    private readonly DatabaseContext _databaseContext;
    private PluginRunContext? _context;
    private PluginRunUser? _user;
    private bool _userLoaded;

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginRunContextAccessor"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文，用于懒加载用户详细信息.</param>
    public PluginRunContextAccessor(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public PluginRunContext? Context => _context;

    /// <inheritdoc/>
    public void Set(PluginRunContext? context)
    {
        _context = context;
    }

    /// <inheritdoc/>
    public async Task<PluginRunUser?> GetUserAsync(CancellationToken cancellationToken)
    {
        if (_context == null || !_context.IsAuthenticated)
        {
            return null;
        }

        if (_userLoaded)
        {
            return _user;
        }

        var user = await _databaseContext.Users
            .FirstOrDefaultAsync(x => x.Id == _context.UserId, cancellationToken)
            .ConfigureAwait(false);

        _user = user == null
            ? null
            : new PluginRunUser
            {
                Id = user.Id,
                UserName = user.UserName,
                NickName = user.NickName,
                Email = user.Email,
                Phone = user.Phone,
                AvatarPath = user.AvatarPath,
                IsAdmin = user.IsAdmin,
            };
        _userLoaded = true;
        return _user;
    }
}
