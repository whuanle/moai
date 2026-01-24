#pragma warning disable CA1031 // 不捕获常规异常类型
#pragma warning disable SA1118 // Parameter should not span multiple lines

using Maomi;
using Microsoft.SemanticKernel;
using MoAI.Infra.BoCha;
using MoAI.Infra.BoCha.Models;
using MoAI.Infra.Exceptions;
using MoAI.Infra.System.Text.Json;
using MoAI.Plugin.Attributes;
using MoAI.Plugin.Models;
using MoAI.Plugin.Plugins.BoCha.Common;
using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MoAI.Plugin.Plugins.BoCha.SemanticRerank;


/// <summary>
/// Semantic Rerank 请求参数
/// </summary>
public class BoChaSemanticRerankParams
{
    /// <summary>
    /// 排序使用的模型版本。
    /// 当前版本模型：
    /// - bocha-semantic-reranker-cn
    /// - bocha-semantic-reranker-en
    /// - gte-rerank
    /// </summary>
    [JsonPropertyName("model")]
    [NativePluginField(
        Key = nameof(Model),
        Description = "排序使用的模型版本",
        FieldType = PluginConfigFieldType.String,
        IsRequired = false,
        ExampleValue = "bocha-semantic-reranker-cn、bocha-semantic-reranker-en、gte-rerank")]
    public string Model { get; init; } = string.Empty;

    /// <summary>
    /// 用户的搜索词。
    /// </summary>
    [JsonPropertyName("query")]
    [NativePluginField(
        Key = nameof(Query),
        Description = "用户的搜索内容",
        FieldType = PluginConfigFieldType.String,
        IsRequired = true,
        ExampleValue = "C#可以做什么")]
    public string Query { get; init; } = string.Empty;

    /// <summary>
    /// 排序返回的Top文档数量。默认与documents数量相同。
    /// </summary>
    [JsonPropertyName("top_n")]
    [NativePluginField(
        Key = nameof(TopN),
        Description = "排序返回的Top文档数量",
        FieldType = PluginConfigFieldType.String,
        IsRequired = false,
        ExampleValue = "10")]
    public int? TopN { get; init; }

    /// <summary>
    /// 排序结果列表是否返回每一条document原文。默认：false。
    /// </summary>
    [JsonPropertyName("return_documents")]
    [NativePluginField(
        Key = nameof(ReturnDocuments),
        Description = "排序结果列表是否返回每一条document原文",
        FieldType = PluginConfigFieldType.String,
        IsRequired = false,
        ExampleValue = "false")]
    public bool? ReturnDocuments { get; init; }
}