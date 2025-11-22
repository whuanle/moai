//#pragma warning disable CA1822 // 将成员标记为 static
//#pragma warning disable CA1031 // 不捕获常规异常类型

//using Maomi;
//using Microsoft.SemanticKernel;
//using MoAI.Infra.Doc2x;
//using MoAI.Infra.Doc2x.Models;
//using MoAI.Infra.Exceptions;
//using MoAI.Infra.Models;
//using MoAI.Infra.Put;
//using MoAI.Infra.System.Text.Json;
//using MoAI.Plugin.Doc2xPdf;
//using MoAI.Plugin.Models;
//using System.ComponentModel;

//namespace MoAI.Plugin.Plugins.Doc2xPdf;

///// <summary>
///// 飞书机器人 WebHook 插件.
///// </summary>
//[Attributes.NativePluginFieldConfigAttribute(
//    "feishu_webhook_text",
//    Name = "飞书机器人发送文本消息",
//    Description = "使用飞书机器人对群聊或人发送消息，消息内容格式为普通文本",
//    Classify = NativePluginClassify.Communication)]
//[InjectOnTransient]
//public class Doc2xPdfPlugin : INativePluginRuntime
//{
//    private static readonly Doc2xPdfPluginConfigValidation Validation = new Doc2xPdfPluginConfigValidation();

//    private readonly IDoc2xClient _client;
//    private readonly IPutClient _putClient;

//    private Doc2xPdfPluginConfig _params = default!;

//    /// <summary>
//    /// Initializes a new instance of the <see cref="Doc2xPdfPlugin"/> class.
//    /// </summary>
//    /// <param name="client"></param>
//    /// <param name="putClient"></param>
//    public Doc2xPdfPlugin(IDoc2xClient client, IPutClient putClient)
//    {
//        _client = client;
//        _putClient = putClient;
//    }

//    /// <inheritdoc/>
//    public async Task<string?> CheckConfigAsync(string config)
//    {
//        try
//        {
//            var objectParams = System.Text.Json.JsonSerializer.Deserialize<Doc2xPdfPluginConfig>(config);
//            var validationResult = await Validation.ValidateAsync(objectParams!);
//            if (validationResult.IsValid)
//            {
//                return string.Empty;
//            }
//            else
//            {
//                return string.Join(";", validationResult.Errors.Select(e => new KeyValueString
//                {
//                    Key = e.PropertyName,
//                    Value = e.ErrorMessage
//                }));
//            }
//        }
//        catch (Exception ex)
//        {
//            return $"参数解析失败: {ex.Message}";
//        }
//    }

//    /// <inheritdoc/>
//    public async Task<Type> GetConfigTypeAsync()
//    {
//        await Task.CompletedTask;

//        return typeof(Doc2xPdfPluginConfig);
//    }

//    /// <inheritdoc/>
//    public async Task<string> GetParamsExampleValue()
//    {
//        await Task.CompletedTask;
//        return System.Text.Json.JsonSerializer.Serialize(
//            new Doc2xPdfPluginParam
//            {
//                Url = "https://example.com/sample.pdf"
//            },
//            JsonSerializerOptionValues.UnsafeRelaxedJsonEscaping);
//    }

//    /// <inheritdoc/>
//    public async Task ImportConfigAsync(string config)
//    {
//        await Task.CompletedTask;

//        var objectParams = System.Text.Json.JsonSerializer.Deserialize<Doc2xPdfPluginConfig>(config);
//        _params = objectParams!;
//    }

//    /// <summary>
//    /// 将pdf转换为可编辑文档.
//    /// </summary>
//    /// <param name="url"></param>
//    /// <returns></returns>
//    [KernelFunction("invoke")]
//    [Description("将pdf转换为markdown")]
//#pragma warning disable CA1054 // 类 URI 参数不应为字符串
//    public async Task<Doc2xRunResult> InvokeAsync([Description("pdf文件地址")] string url)
//    {
//        // 下载 pdf 到本地
//        var tmpFile = System.IO.Path.GetTempFileName();
//        using var tmpFileStream = System.IO.File.Create(tmpFile);
//        try
//        {
//            using var stream = await _putClient.DownloadAsync(url);
//            await stream.CopyToAsync(tmpFileStream);
//            await tmpFileStream.FlushAsync();
//            tmpFileStream.Seek(0, SeekOrigin.Begin);
//        }
//        catch (Exception ex)
//        {
//            return new Doc2xRunResult
//            {
//                Code = "Error",
//                ErrorMessage = $"下载文件失败: {ex.Message}"
//            };
//        }

//        // 获取预上传接口
//        var preUploadResult = await _client.PreuploadAsync(_params.Key, new EmptyDto());
//        if (preUploadResult.Code != "success")
//        {
//            return new Doc2xRunResult
//            {
//                Code = preUploadResult.Code,
//                ErrorMessage = preUploadResult.Msg!
//            };
//        }

//        // 上传文件
//        await _putClient.PutAsync(preUploadResult.Data.Url!, new Refit.StreamPart(tmpFileStream, "moai.pdf"));

//        // 轮询判断文件是否上传完成
//        CancellationTokenSource? cts = new CancellationTokenSource(TimeSpan.FromMinutes(10));
//        Doc2xParseStatusResponse? uploadStatus = default!;
//        while (!cts.IsCancellationRequested)
//        {
//            var checkResult = await _client.GetParseStatusAsync(_params.Key, preUploadResult.Data.Uid!);
//            if (checkResult.Code != "success")
//            {
//                return new Doc2xRunResult
//                {
//                    Code = checkResult.Code,
//                    ErrorMessage = checkResult.Msg!
//                };
//            }

//            if (checkResult.Data.Progress == 100)
//            {
//                uploadStatus = checkResult;
//                break;
//            }
//        }

//        if (uploadStatus == null)
//        {
//            return new Doc2xRunResult
//            {
//                Code = "Error",
//                ErrorMessage = "文件上传超时"
//            };
//        }

//        try
//        {
//            var parseResult = await _client.ExportParseResultAsync(_params.Key, new Doc2xExportRequest
//            {
//                Uid = preUploadResult.Data.Uid!,
//                To = "md",
//                FormulaMode = "normal"
//            });

//            if (parseResult.Code != "success")
//            {
//                return new Doc2xRunResult
//                {
//                    Code = parseResult.Code,
//                    ErrorMessage = parseResult.Msg!
//                };
//            }

//            // 轮询获取导出结果
//            Doc2xExportResultResponse? exportResult = default!;
//            cts = new CancellationTokenSource(TimeSpan.FromMinutes(10));
//            while (!cts.IsCancellationRequested)
//            {
//                var checkResult = await _client.GetExportResultAsync(_params.Key, parseResult.Data.Uid!);
//                if (checkResult.Code != "success")
//                {
//                    return new Doc2xRunResult
//                    {
//                        Code = checkResult.Code,
//                        ErrorMessage = checkResult.Msg!
//                    };
//                }
//                if (checkResult.Data.Progress == 100)
//                {
//                    exportResult = checkResult;
//                    break;
//                }
//            }

//            return default!;
//        }
//        catch (Exception ex)
//        {
//            return new Doc2xRunResult
//            {
//                Code = "faild",
//                Msg = ex.Message
//            };
//        }
//    }

//    /// <inheritdoc/>
//    public async Task<string> TestAsync(string @params)
//    {
//        try
//        {
//            var str = System.Text.Json.JsonSerializer.Deserialize<string>(@params);
//            var result = await InvokeAsync(str!);
//            return System.Text.Json.JsonSerializer.Serialize(result, JsonSerializerOptionValues.UnsafeRelaxedJsonEscaping);
//        }
//        catch (Exception ex)
//        {
//            throw new BusinessException(ex.Message);
//        }
//    }
//}
