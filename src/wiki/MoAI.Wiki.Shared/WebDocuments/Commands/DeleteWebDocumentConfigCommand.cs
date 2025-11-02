using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Wiki.WebDocuments.Commands;

public class DeleteWebDocumentConfigCommand : IRequest<EmptyCommandResponse>
{
    /// <summary>
    /// 知识库 id.
    /// </summary>
    public int WikiId { get; init; }

    public int WikiWebConfigId { get; init; }

    /// <summary>
    /// 是否删除该配置下的所有网页.
    /// </summary>
    public bool IsDeleteWebDocuments { get; set; }
}