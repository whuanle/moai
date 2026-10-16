using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace I18nScanner;

/// <summary>
/// 基于 Roslyn 扫描 BusinessException 构造点，提取消息字符串.
/// </summary>
public sealed class BusinessMessageScanner
{
    /// <summary>
    /// 扫描一组源文件.
    /// </summary>
    /// <param name="files">源文件绝对路径列表.</param>
    /// <param name="rootPath">用于计算相对路径的根目录.</param>
    /// <returns>按文件与行号排序的提取结果.</returns>
    public static IReadOnlyList<BusinessMessageEntry> Scan(IEnumerable<string> files, string rootPath)
    {
        var entries = new List<BusinessMessageEntry>();
        foreach (var file in files)
        {
            string text;
            try
            {
                text = File.ReadAllText(file);
            }
#pragma warning disable CA1031 // 逐个文件容错继续扫描，失败原因打到 stderr
            catch (Exception ex)
#pragma warning restore CA1031
            {
                Console.Error.WriteLine($"[warn] 读取失败 {file}: {ex.Message}");
                continue;
            }

            var root = CSharpSyntaxTree.ParseText(text, new CSharpParseOptions(LanguageVersion.Latest)).GetCompilationUnitRoot();
            foreach (var creation in root.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
            {
                if (!IsBusinessException(creation.Type))
                {
                    continue;
                }

                entries.Add(Extract(creation, Path.GetRelativePath(rootPath, file)));
            }
        }

        return entries
            .OrderBy(e => e.File, StringComparer.Ordinal)
            .ThenBy(e => e.Line)
            .ToList();
    }

    private static string GetSimpleTypeName(TypeSyntax type)
    {
        return type switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
            AliasQualifiedNameSyntax alias => alias.Name.Identifier.ValueText,
            GenericNameSyntax generic => generic.Identifier.ValueText,
            _ => type.ToString()
        };
    }

    private static bool IsBusinessException(TypeSyntax type)
    {
        return GetSimpleTypeName(type) == "BusinessException";
    }

    private static BusinessMessageEntry Extract(ObjectCreationExpressionSyntax creation, string relativeFile)
    {
        var line = GetLine(creation);
        var argumentList = creation.ArgumentList;
        if (argumentList != null)
        {
            foreach (var argument in argumentList.Arguments)
            {
                if (argument.NameColon != null)
                {
                    continue;
                }

                var (template, kind) = ExtractTemplate(argument.Expression);
                if (kind == MessageExpressionKind.Unresolved)
                {
                    continue;
                }

                return new BusinessMessageEntry
                {
                    Template = template,
                    Kind = kind,
                    File = relativeFile,
                    Line = line,
                    Expression = argument.Expression.ToString()
                };
            }
        }

        // 没有任何可解析的消息参数：报告第一个位置参数或整个构造表达式
        var fallback = argumentList != null && argumentList.Arguments.Count > 0
            ? argumentList.Arguments[0].Expression
            : creation;
        return new BusinessMessageEntry
        {
            Template = null,
            Kind = MessageExpressionKind.Unresolved,
            File = relativeFile,
            Line = line,
            Expression = Truncate(fallback.ToString())
        };
    }

    private static int GetLine(SyntaxNode node)
    {
        var lineSpan = node.GetLocation().GetLineSpan();
        if (!lineSpan.IsValid)
        {
            return 0;
        }

        return lineSpan.StartLinePosition.Line + 1;
    }

    private static (string? Template, MessageExpressionKind Kind) ExtractTemplate(ExpressionSyntax expression)
    {
        var parts = new List<string>();
        var holeCount = 0;
        if (!TryFlatten(expression, parts, ref holeCount))
        {
            return (null, MessageExpressionKind.Unresolved);
        }

        var template = string.Concat(parts);
        if (string.IsNullOrWhiteSpace(template))
        {
            return (null, MessageExpressionKind.Unresolved);
        }

        return (template, holeCount > 0 ? MessageExpressionKind.Interpolated : MessageExpressionKind.Literal);
    }

    private static bool TryFlatten(ExpressionSyntax expression, List<string> parts, ref int holeCount)
    {
        switch (expression)
        {
            case LiteralExpressionSyntax literal when literal.IsKind(SyntaxKind.StringLiteralExpression):

                // 字面量是运行时 Message 的原样文本，花括号保持不动才能与 key 精确匹配
                parts.Add(literal.Token.ValueText);
                return true;

            case InterpolatedStringExpressionSyntax interpolated:
                foreach (var content in interpolated.Contents)
                {
                    switch (content)
                    {
                        case InterpolatedStringTextSyntax text:
                            parts.Add(EscapeFormatBraces(text.TextToken.ValueText));
                            break;

                        case InterpolationSyntax hole:
                            parts.Add($"{{{holeCount}}}");
                            holeCount++;

                            // 对齐与格式子句原样保留（其 ToString 已含前置逗号/冒号）
                            if (hole.AlignmentClause != null)
                            {
                                parts.Add(hole.AlignmentClause.ToString());
                            }

                            if (hole.FormatClause != null)
                            {
                                parts.Add(hole.FormatClause.ToString());
                            }

                            break;

                        default:
                            return false;
                    }
                }

                return true;

            case BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.AddExpression):
                return TryFlatten(binary.Left, parts, ref holeCount) && TryFlatten(binary.Right, parts, ref holeCount);

            case ParenthesizedExpressionSyntax parenthesized:
                return TryFlatten(parenthesized.Expression, parts, ref holeCount);

            default:
                return false;
        }
    }

    private static string EscapeFormatBraces(string text)
    {
        return text.Replace("{", "{{", StringComparison.Ordinal).Replace("}", "}}", StringComparison.Ordinal);
    }

    private static string Truncate(string text)
    {
        const int MaxLength = 160;
        if (text.Length <= MaxLength)
        {
            return text;
        }

        return text[..MaxLength] + "…";
    }
}
