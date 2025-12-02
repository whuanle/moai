using MoAI.Infra.Feishu.Models;
using System.Text;
using System.Text.Json;

namespace MoAI.Infra.Feishu;

/// <summary>
/// 飞书文档转Markdown.
/// </summary>
public static class FeishuWikiToMarkdown
{
    public static void ToMarkdown(StringBuilder stringBuilder, IReadOnlyCollection<Block> blocks)
    {
        foreach (var block in blocks)
        {
            switch (block.BlockType)
            {
                // 1：页面 Block
                case 1:
                    stringBuilder.AppendLine($"# {GetTextFromElement(block.Page)}");
                    break;
                // 2：文本 Block
                case 2:
                    stringBuilder.AppendLine(GetTextFromElement(block.Text));
                    break;
                // 3：标题 1 Block
                case 3:
                    stringBuilder.AppendLine($"# {GetTextFromElement(block.Heading1)}");
                    break;
                // 4：标题 2 Block
                case 4:
                    stringBuilder.AppendLine($"## {GetTextFromElement(block.Heading2)}");
                    break;
                // 5：标题 3 Block
                case 5:
                    stringBuilder.AppendLine($"### {GetTextFromElement(block.Heading3)}");
                    break;
                // 6：标题 4 Block
                case 6:
                    stringBuilder.AppendLine($"#### {GetTextFromElement(block.Heading4)}");
                    break;
                // 7：标题 5 Block
                case 7:
                    stringBuilder.AppendLine($"##### {GetTextFromElement(block.Heading5)}");
                    break;
                // 8：标题 6 Block
                case 8:
                    stringBuilder.AppendLine($"###### {GetTextFromElement(block.Heading6)}");
                    break;
                // 9：标题 7 Block
                case 9:
                    stringBuilder.AppendLine($"####### {GetTextFromElement(block.Heading7)}");
                    break;
                // 10：标题 8 Block
                case 10:
                    stringBuilder.AppendLine($"######## {GetTextFromElement(block.Heading8)}");
                    break;
                // 11：标题 9 Block
                case 11:
                    stringBuilder.AppendLine($"######### {GetTextFromElement(block.Heading9)}");
                    break;
                // 12：无序列表 Block
                case 12:
                    stringBuilder.AppendLine($"- {GetTextFromElement(block.Bullet)}");
                    break;
                // 13：有序列表 Block
                case 13:
                    stringBuilder.AppendLine($"1. {GetTextFromElement(block.Ordered)}");
                    break;
                // 14：代码块 Block
                case 14:
                    var (code, lang) = GetCodeBlockDetails(block.Code);
                    stringBuilder.AppendLine($"```{lang}");
                    stringBuilder.AppendLine(code);
                    stringBuilder.AppendLine("```");
                    break;
                // 15：引用 Block
                case 15:
                    stringBuilder.AppendLine($"> {GetTextFromElement(block.Quote)}");
                    break;
                // 17：待办事项 Block
                case 17:
                    var (text, isDone) = GetTodoText(block.Todo);
                    stringBuilder.AppendLine(isDone ? $"- [x] {text}" : $"- [ ] {text}");
                    break;
                // 22：分割线 Block
                case 22:
                    stringBuilder.AppendLine("---");
                    break;
                default:
                    // 对于其他未处理的块类型，可以选择忽略或添加占位符
                    break;
            }
        }
    }

    public static string ToMarkdown(IReadOnlyCollection<Block> blocks)
    {
        StringBuilder stringBuilder = new StringBuilder();
        ToMarkdown(stringBuilder, blocks);
        return stringBuilder.ToString();
    }

    private static string GetTextFromElement(JsonElement? element)
    {
        if (!element.HasValue) return string.Empty;

        var sb = new StringBuilder();
        if (element.Value.TryGetProperty("elements", out var elements))
        {
            foreach (var el in elements.EnumerateArray())
            {
                if (el.TryGetProperty("text_run", out var textRun) && textRun.TryGetProperty("content", out var content))
                {
                    sb.Append(content.GetString());
                }
            }
        }
        return sb.ToString();
    }

    private static (string text, bool isDone) GetTodoText(JsonElement? element)
    {
        if (!element.HasValue) return (string.Empty, false);

        bool isDone = false;
        if (element.Value.TryGetProperty("style", out var style) && style.TryGetProperty("done", out var done))
        {
            isDone = done.GetBoolean();
        }

        return (GetTextFromElement(element), isDone);
    }

    private static (string code, string language) GetCodeBlockDetails(JsonElement? element)
    {
        if (!element.HasValue) return (string.Empty, string.Empty);

        string language = string.Empty;
        if (element.Value.TryGetProperty("style", out var style) && style.TryGetProperty("language", out var langProp))
        {
            language = langProp.GetInt32() switch
            {
                1 => "plaintext",
                2 => "abap",
                3 => "ada",
                4 => "apache",
                5 => "apex",
                6 => "assembly",
                7 => "bash",
                8 => "csharp",
                9 => "cpp",
                10 => "c",
                11 => "cobol",
                12 => "css",
                13 => "coffeescript",
                14 => "d",
                15 => "dart",
                16 => "delphi",
                17 => "django",
                18 => "dockerfile",
                19 => "erlang",
                20 => "fortran",
                21 => "foxpro",
                22 => "go",
                23 => "groovy",
                24 => "html",
                25 => "htmlbars",
                26 => "http",
                27 => "haskell",
                28 => "json",
                29 => "java",
                30 => "javascript",
                31 => "julia",
                32 => "kotlin",
                33 => "latex",
                34 => "lisp",
                35 => "logo",
                36 => "lua",
                37 => "matlab",
                38 => "makefile",
                39 => "markdown",
                40 => "nginx",
                41 => "objectivec",
                42 => "openedgeabl",
                43 => "php",
                44 => "perl",
                45 => "postscript",
                46 => "powershell",
                47 => "prolog",
                48 => "protobuf",
                49 => "python",
                50 => "r",
                51 => "rpg",
                52 => "ruby",
                53 => "rust",
                54 => "sas",
                55 => "scss",
                56 => "sql",
                57 => "scala",
                58 => "scheme",
                59 => "scratch",
                60 => "shell",
                61 => "swift",
                62 => "thrift",
                63 => "typescript",
                64 => "vbscript",
                65 => "vb",
                66 => "xml",
                67 => "yaml",
                68 => "cmake",
                69 => "diff",
                70 => "gherkin",
                71 => "graphql",
                72 => "glsl",
                73 => "properties",
                74 => "solidity",
                75 => "toml",
                _ => string.Empty
            };
        }

        return (GetTextFromElement(element), language);
    }
}