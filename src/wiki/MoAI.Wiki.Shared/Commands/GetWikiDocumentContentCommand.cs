using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Wiki.Commands;

/// <summary>
/// 读取知识库文档已提取的完整内容（markdown），仅团队成员可访问.
/// 文档内容区在 detail 只拿到预览（截断）时，通过本命令在用户点击「全部加载」时取回全文.
/// </summary>
public class GetWikiDocumentContentCommand : IRequest<SimpleString>, IModelValidator<GetWikiDocumentContentCommand>
{
    /// <summary>
    /// 知识库 id，由 Controller 从路由参数回填.
    /// </summary>
    public long WikiId { get; init; }

    /// <summary>
    /// 文档 id，由 Controller 从路由参数回填.
    /// </summary>
    public long DocumentId { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<GetWikiDocumentContentCommand> validate)
    {
        // WikiId/DocumentId 由 Controller 从路由参数回填，自动验证发生在回填之前，因此此处只校验请求体字段（无）.
    }
}
