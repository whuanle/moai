using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Models;

namespace MoAI.Login.Queries;

public class QueryRepeatedUserNameCommandHandler : IRequestHandler<QueryRepeatedUserNameCommand, SimpleBool>
{
    private readonly DatabaseContext _dbContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryRepeatedUserNameCommandHandler"/> class.
    /// </summary>
    /// <param name="dbContext"></param>
    public QueryRepeatedUserNameCommandHandler(DatabaseContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<SimpleBool> Handle(QueryRepeatedUserNameCommand request, CancellationToken cancellationToken)
    {
        var existingUser = await _dbContext.Users
        .Where(u => u.UserName == request.UserName || u.Email == request.UserName || u.Phone == request.UserName)
        .Select(u => new { u.UserName, u.Email, u.Phone })
        .FirstOrDefaultAsync();

        if (existingUser != null)
        {
            return new SimpleBool() { Value = true };
        }

        return new SimpleBool() { Value = false };
    }
}
