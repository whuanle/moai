using System.Text.Json;
using System.Text.Json.Serialization;

namespace MoAI.Infra.Feishu.Models;

/// <summary>
/// 文档块。
/// </summary>
public class Block
{
    /// <summary>
    /// 子块的唯一标识。
    /// </summary>
    [JsonPropertyName("block_id")]
    public string BlockId { get; set; } = string.Empty;

    /// <summary>
    /// 子块的父块 ID。
    /// </summary>
    [JsonPropertyName("parent_id")]
    public string? ParentId { get; set; }

    /// <summary>
    /// 子块的子块 ID 列表。
    /// </summary>
    [JsonPropertyName("children")]
    public List<string> Children { get; set; } = [];

    /// <summary>
    /// Block 类型。<br />
    /// 1：页面 Block<br />
    /// 2：文本 Block<br />
    /// 3：标题 1 Block<br />
    /// 4：标题 2 Block<br />
    /// 5：标题 3 Block<br />
    /// 6：标题 4 Block<br />
    /// 7：标题 5 Block<br />
    /// 8：标题 6 Block<br />
    /// 9：标题 7 Block<br />
    /// 10：标题 8 Block<br />
    /// 11：标题 9 Block<br />
    /// 12：无序列表 Block<br />
    /// 13：有序列表 Block<br />
    /// 14：代码块 Block<br />
    /// 15：引用 Block<br />
    /// 17：待办事项 Block<br />
    /// 18：多维表格 Block<br />
    /// 19：高亮块 Block<br />
    /// 20：会话卡片 Block<br />
    /// 21：流程图  UML Block<br />
    /// 22：分割线 Block。为空结构体，需传入 {} 创建分割线 Block。<br />
    /// 23：文件 Block<br />
    /// 24：分栏 Block<br />
    /// 25：分栏列 Block<br />
    /// 26：内嵌网页 Block<br />
    /// 27：图片 Block<br />
    /// 28：开放平台小组件 Block<br />
    /// 29：思维笔记 Block<br />
    /// 30：电子表格 Block<br />
    /// 31：表格 Block。了解如何在文档中插入表格，参考文档常见问题-如何插入表格并往单元格填充内容。<br />
    /// 32：表格单元格 Block<br />
    /// 33：视图 Block<br />
    /// 34：引用容器 Block。为空结构体，需传入 {} 创建引用容器 Block。<br />
    /// 35：任务 Block<br />
    /// 36：OKR Block<br />
    /// 37：OKR Objective Block<br />
    /// 38：OKR Key Result Block<br />
    /// 39：OKR 进展 Block<br />
    /// 40：文档小组件 Block<br />
    /// 41：Jira 问题 Block<br />
    /// 42：Wiki 子目录 Block<br />
    /// 43：画板 Block<br />
    /// 44：议程 Block<br />
    /// 45：议程项 Block<br />
    /// 46：议程项标题 Block<br />
    /// 47：议程项内容 Block<br />
    /// 48：链接预览 Block<br />
    /// 49：源同步块，仅支持查询<br />
    /// 50：引用同步块，仅支持查询。获取引用同步块内容详见：如何获取引用同步块的内容<br />
    /// 51：Wiki 新版子目录<br />
    /// 52：AI 模板 Block，仅支持查询<br />
    /// 999：未支持 Block
    /// </summary>
    [JsonPropertyName("block_type")]
    public int BlockType { get; set; }

    /// <summary>
    /// 文本 Block。
    /// </summary>
    [JsonPropertyName("text")]
    public JsonElement? Text { get; set; }

    /// <summary>
    /// 页面 Block。
    /// </summary>
    [JsonPropertyName("page")]
    public JsonElement? Page { get; set; }

    /// <summary>
    /// 一级标题 Block。
    /// </summary>
    [JsonPropertyName("heading1")]
    public JsonElement? Heading1 { get; set; }

    /// <summary>
    /// 二级标题 Block。
    /// </summary>
    [JsonPropertyName("heading2")]
    public JsonElement? Heading2 { get; set; }

    /// <summary>
    /// 三级标题 Block。
    /// </summary>
    [JsonPropertyName("heading3")]
    public JsonElement? Heading3 { get; set; }

    /// <summary>
    /// 四级标题 Block。
    /// </summary>
    [JsonPropertyName("heading4")]
    public JsonElement? Heading4 { get; set; }

    /// <summary>
    /// 五级标题 Block。
    /// </summary>
    [JsonPropertyName("heading5")]
    public JsonElement? Heading5 { get; set; }

    /// <summary>
    /// 六级标题 Block。
    /// </summary>
    [JsonPropertyName("heading6")]
    public JsonElement? Heading6 { get; set; }

    /// <summary>
    /// 七级标题 Block。
    /// </summary>
    [JsonPropertyName("heading7")]
    public JsonElement? Heading7 { get; set; }

    /// <summary>
    /// 八级标题 Block。
    /// </summary>
    [JsonPropertyName("heading8")]
    public JsonElement? Heading8 { get; set; }

    /// <summary>
    /// 九级标题 Block。
    /// </summary>
    [JsonPropertyName("heading9")]
    public JsonElement? Heading9 { get; set; }

    /// <summary>
    /// 无序列表 Block。
    /// </summary>
    [JsonPropertyName("bullet")]
    public JsonElement? Bullet { get; set; }

    /// <summary>
    /// 有序列表 Block。
    /// </summary>
    [JsonPropertyName("ordered")]
    public JsonElement? Ordered { get; set; }

    /// <summary>
    /// 代码块 Block。
    /// </summary>
    [JsonPropertyName("code")]
    public JsonElement? Code { get; set; }

    /// <summary>
    /// 引用 Block。
    /// </summary>
    [JsonPropertyName("quote")]
    public JsonElement? Quote { get; set; }

    /// <summary>
    /// 待办事项 Block。
    /// </summary>
    [JsonPropertyName("todo")]
    public JsonElement? Todo { get; set; }

    /// <summary>
    /// 其他未支持的 Block 类型。
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}