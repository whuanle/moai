using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.KnowledgeGraph.Queries.Responses;

namespace MoAI.KnowledgeGraph.Commands;

/// <summary>
/// 测试图数据库连通性（系统设置开启前预检），仅超级管理员可调用.
/// 字段均为可选覆盖：留空时回退系统设置中已保存的连接信息，便于保存前用表单值直接测试.
/// </summary>
public class TestKnowledgeGraphConnectionCommand : IRequest<TestKnowledgeGraphConnectionCommandResponse>, IModelValidator<TestKnowledgeGraphConnectionCommand>
{
    /// <summary>
    /// 连接地址（如 bolt://127.0.0.1:7687），空=用已保存值.
    /// </summary>
    public string? Uri { get; init; }

    /// <summary>
    /// 用户名，空=用已保存值.
    /// </summary>
    public string? Username { get; init; }

    /// <summary>
    /// 密码，空=用已保存值.
    /// </summary>
    public string? Password { get; init; }

    /// <summary>
    /// 方言：memgraph / neo4j，空=用已保存值（再缺省 memgraph）.
    /// </summary>
    public string? Dialect { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<TestKnowledgeGraphConnectionCommand> validate)
    {
        validate.RuleFor(x => x.Uri)
            .MaximumLength(200).WithMessage("图数据库连接地址最长 200 个字符.")
            .Must(x => x == null || x.Trim().Length == 0 || IsValidUri(x))
            .WithMessage("图数据库连接地址无效，须形如 bolt://127.0.0.1:7687.");
        validate.RuleFor(x => x.Username).MaximumLength(100).WithMessage("用户名最长 100 个字符.");
        validate.RuleFor(x => x.Password).MaximumLength(200).WithMessage("密码最长 200 个字符.");
        validate.RuleFor(x => x.Dialect)
            .Must(x => x == null || x.Trim().Length == 0 || x.Trim().Equals("memgraph", StringComparison.OrdinalIgnoreCase) || x.Trim().Equals("neo4j", StringComparison.OrdinalIgnoreCase))
            .WithMessage("图数据库方言必须是 memgraph 或 neo4j.");
    }

    /// <summary>
    /// 校验连接地址为驱动支持的绝对 bolt/neo4j scheme.
    /// </summary>
    private static bool IsValidUri(string value)
    {
        if (!System.Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri))
        {
            return false;
        }

        return uri.Scheme.StartsWith("bolt", StringComparison.OrdinalIgnoreCase)
            || uri.Scheme.StartsWith("neo4j", StringComparison.OrdinalIgnoreCase);
    }
}
