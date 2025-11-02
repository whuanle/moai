using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Helpers;
using MoAI.Infra.Models;
using MoAI.Infra.Services;
using System.Text;

namespace MoAI.Admin.User.Commands;

/// <summary>
/// <inheritdoc cref="ResetUserPasswordCommand"/>
/// </summary>
public class ResetUserPasswordCommandHandler : IRequestHandler<ResetUserPasswordCommand, SimpleString>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IRsaProvider _rsaProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="ResetUserPasswordCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="rsaProvider"></param>
    public ResetUserPasswordCommandHandler(DatabaseContext databaseContext, IRsaProvider rsaProvider)
    {
        _databaseContext = databaseContext;
        _rsaProvider = rsaProvider;
    }

    /// <inheritdoc/>
    public async Task<SimpleString> Handle(ResetUserPasswordCommand request, CancellationToken cancellationToken)
    {
        var user = await _databaseContext.Users
            .Where(x => x.Id == request.UserId)
            .FirstOrDefaultAsync(cancellationToken);

        if (user is null)
        {
            throw new BusinessException("未查找到用户.") { StatusCode = 404 };
        }

        string defaultPassword = Encoding.UTF8.GetString(Convert.FromBase64String("YWJjZDEyMzQ1Ng=="));
        var (hashPassword, salt) = PBKDF2Helper.ToHash(defaultPassword);

        user.PasswordSalt = salt;
        user.Password = hashPassword;

        await _databaseContext.SaveChangesAsync(cancellationToken);
        return new SimpleString { Value = defaultPassword };
    }
}