using FluentValidation;
using MediatR;
using MoAI.Wiki.Queries.Responses;

namespace MoAI.Wiki.Queries;

/// <summary>
/// 查询知识库上传文件大小上限，任意登录用户可访问（前端上传前预检用）.
/// </summary>
public class QueryWikiUploadLimitCommand : IRequest<QueryWikiUploadLimitCommandResponse>, IModelValidator<QueryWikiUploadLimitCommand>
{
    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryWikiUploadLimitCommand> validate)
    {
        // 无参数，无需校验
    }
}
