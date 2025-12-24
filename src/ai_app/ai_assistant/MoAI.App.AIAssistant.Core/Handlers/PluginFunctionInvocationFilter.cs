#pragma warning disable SKEXP0001 // 类型仅用于评估，在将来的更新中可能会被更改或删除。取消此诊断以继续。
#pragma warning disable SKEXP0040 // 类型仅用于评估，在将来的更新中可能会被更改或删除。取消此诊断以继续。
#pragma warning disable CA1849 // 当在异步方法中时，调用异步方法

using Microsoft.SemanticKernel;
using MoAI.AI.Models;
using MoAI.Infra.Extensions;
using MoAI.Infra.Models;
using System.Diagnostics;
using System.Reflection;

namespace MoAI.App.AIAssistant.Handlers;

/// <summary>
/// 处理函数调用.
/// </summary>
internal class PluginFunctionInvocationFilter : IFunctionInvocationFilter
{
    private static readonly MethodInfo GetValueMethodInfo = typeof(PluginFunctionInvocationFilter).GetMethod(nameof(GetJsonValue), BindingFlags.Static | BindingFlags.NonPublic)!;

    private readonly ProcessingAiAssistantChatContext _processingAiAssistantChatContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginFunctionInvocationFilter"/> class.
    /// </summary>
    /// <param name="processingAiAssistantChatContext"></param>
    public PluginFunctionInvocationFilter(ProcessingAiAssistantChatContext processingAiAssistantChatContext)
    {
        _processingAiAssistantChatContext = processingAiAssistantChatContext;
    }

    /// <inheritdoc/>
    public async Task OnFunctionInvocationAsync(FunctionInvocationContext context, Func<FunctionInvocationContext, Task> next)
    {
        var pluginName = context.Function.PluginName!;
        var pluginType = pluginName.StartsWith("wiki_", StringComparison.CurrentCultureIgnoreCase)
            ? PluginType.WikiPlugin
            : PluginType.NativePlugin;

        var choice = _processingAiAssistantChatContext.Choices.LastOrDefault();

        // 一般不会到这一步
        if (choice == null || choice.StreamType != AiProcessingChatStreamType.Plugin)
        {
            Debug.Assert(false, "PluginFunctionInvocationFilter: choice is null or not plugin");
            choice = new DefaultAiProcessingChoice
            {
                StreamType = AiProcessingChatStreamType.Plugin,
                StreamState = AiProcessingChatStreamState.Processing,
                PluginCall = new DefaultAiProcessingPluginCall
                {
                    PluginKey = pluginName,
                    PluginName = _processingAiAssistantChatContext.PluginKeyNames.FirstOrDefault(x => x.Key == pluginName).Value ?? string.Empty,
                    PluginType = pluginType,
                    Message = string.Empty,
                    Params = Array.Empty<KeyValueString>(),
                    Result = string.Empty
                }
            };

            _processingAiAssistantChatContext.Choices.Add(choice);
        }

        choice.StreamState = AiProcessingChatStreamState.Processing;
        choice.IsPush = false;

        try
        {
            choice.PluginCall!.Params = context.Arguments.Select(x=>new KeyValueString
            {
                Key = x.Key,
                Value = x.Value.ToJsonString()
            }).ToArray();

            await next(context);

            choice.StreamState = AiProcessingChatStreamState.End;
            choice.PluginCall!.Result = GetValue(context.Result);
        }
        catch (Exception ex)
        {
            choice.StreamState = AiProcessingChatStreamState.Error;
            choice.PluginCall!.Message = ex.Message;
            throw;
        }
    }

    private static string GetValue(FunctionResult functionResult)
    {
        var type = functionResult.ValueType;
        if (type == null)
        {
            return functionResult.ToString() ?? string.Empty;
        }

        var typeCode = TypeInfo.GetTypeCode(type);

        if (typeCode == TypeCode.Object)
        {
            var json = GetValueMethodInfo.MakeGenericMethod(type).Invoke(null, new[] { functionResult });
            return json!.ToString()!;
        }
        else if (typeCode == TypeCode.DBNull)
        {
            return string.Empty;
        }
        else
        {
            return functionResult.ToString() ?? string.Empty;
        }
    }

    private static string GetJsonValue<T>(FunctionResult functionResult)
    {
        var value = functionResult.GetValue<T>().ToJsonString();
        return value;
    }
}