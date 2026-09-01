using MediatR;
using MoAI.Settings.Queries.Responses;

namespace MoAI.Settings.Queries;

/// <summary>
/// 查询全部设置项.
/// </summary>
public class QuerySettingsCommand : IRequest<QuerySettingsCommandResponse>
{
}
