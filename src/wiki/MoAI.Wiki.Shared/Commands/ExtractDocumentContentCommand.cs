using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Wiki.Commands;

/// <summary>
/// 提取知识库文档内容（Maomi.ToMarkdown 抽取 markdown 并入库），仅团队成员可操作.
/// 文档上传后默认未提取内容，需先执行本命令生成内容，之后才能进行切割。
/// </summary>
public class ExtractDocumentContentCommand : IRequest<EmptyCommandResponse>, IModelValidator<ExtractDocumentContentCommand>
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
    public static void Validate(AbstractValidator<ExtractDocumentContentCommand> validate)
    {
        // WikiId/DocumentId 由 Controller 从路由参数回填，自动验证发生在回填之前，因此此处只校验请求体字段（无）.
    }
}
