using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Paddleocr;
using MoAI.Infra.System.Text.Json;
using MoAI.Plugin;
using MoAI.Plugin.Plugins;
using MoAI.Storage.Commands;
using MoAI.Wiki.Plugins.Paddleocr.Commands;
using MoAI.Wiki.Plugins.Paddleocr.Models;
using System.Text.Json;
using static Org.BouncyCastle.Math.EC.ECCurve;

namespace MoAI.Wiki.Plugins.Paddleocr.Handlers;

/// <summary>
/// <inheritdoc cref="PreviewPaddleocrDocumentCommand"/>
/// </summary>
public class PreviewPaddleocrDocumentCommandHandler : IRequestHandler<PreviewPaddleocrDocumentCommand, PaddleocrPreviewResult>
{
    private static readonly string[] PaddleocrTemplateKeys = new[]
    {
        "paddleocr_ocr",
        "paddleocr_vl",
        "paddleocr_structure_v3"
    };

    private readonly DatabaseContext _databaseContext;
    private readonly IMediator _mediator;
    private readonly INativePluginFactory _nativePluginFactory;
    private readonly IServiceProvider _serviceProvider;
    private readonly IPaddleocrClient _paddleocrClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="PreviewPaddleocrDocumentCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="mediator"></param>
    /// <param name="nativePluginFactory"></param>
    /// <param name="serviceProvider"></param>
    /// <param name="paddleocrClient"></param>
    public PreviewPaddleocrDocumentCommandHandler(
        DatabaseContext databaseContext,
        IMediator mediator,
        INativePluginFactory nativePluginFactory,
        IServiceProvider serviceProvider,
        IPaddleocrClient paddleocrClient)
    {
        _databaseContext = databaseContext;
        _mediator = mediator;
        _nativePluginFactory = nativePluginFactory;
        _serviceProvider = serviceProvider;
        _paddleocrClient = paddleocrClient;
    }

    /// <inheritdoc/>
    public async Task<PaddleocrPreviewResult> Handle(PreviewPaddleocrDocumentCommand request, CancellationToken cancellationToken)
    {
        var teamId = await _databaseContext.Wikis.Where(x => x.Id == request.WikiId).Select(x => x.TeamId).FirstOrDefaultAsync(cancellationToken);

        // 判断插件授权
        var hasPermission = await _databaseContext.Plugins
            .Where(x => x.Id == request.PluginId)
            .Where(x => x.IsPublic || _databaseContext.PluginAuthorizations.Any(auth => auth.PluginId == x.Id && auth.TeamId == teamId))
            .AnyAsync(cancellationToken);

        if (!hasPermission)
        {
            throw new BusinessException("没有使用该插件的权限") { StatusCode = 403 };
        }

        var pluginInfo = await _databaseContext.Plugins
            .Join(
            _databaseContext.PluginNatives,
            plugin => plugin.PluginId,
            native => native.Id,
            (plugin, native) => new { plugin, native })
            .Where(x => x.plugin.Id == request.PluginId
                && PaddleocrTemplateKeys.Contains(x.native.TemplatePluginKey))
            .Select(x => new
            {
                x.plugin.Id,
                x.native.TemplatePluginKey,
                x.native.Config
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (pluginInfo == null)
        {
            throw new BusinessException("插件不存在或不是飞桨 OCR 插件") { StatusCode = 404 };
        }

        // 获取文件信息
        var fileInfo = await _databaseContext.Files
            .Where(x => x.Id == request.FileId)
            .FirstOrDefaultAsync(cancellationToken);

        if (fileInfo == null)
        {
            throw new BusinessException("文件不存在") { StatusCode = 404 };
        }

        // 获取插件模板信息
        var templateInfo = _nativePluginFactory.GetPluginByKey(pluginInfo.TemplatePluginKey);
        if (templateInfo == null)
        {
            throw new BusinessException("插件模板不存在") { StatusCode = 404 };
        }

        // 下载文件并转为 Base64
        var tempFilePath = await _mediator.Send(new DownloadFileCommand { FileId = request.FileId });
        var fileBytes = await File.ReadAllBytesAsync(tempFilePath.LocalFilePath, cancellationToken);
        var fileBase64 = Convert.ToBase64String(fileBytes);

        // 创建插件实例并执行
        var pluginInstance = (INativePluginRuntime)_serviceProvider.GetRequiredService(templateInfo.Type);
        await pluginInstance.ImportConfigAsync(pluginInfo.Config);
        var paddleocrPlugin = pluginInstance as IPaddleocrPlugin;

        var ocrResult = await paddleocrPlugin!.OcrAsync(fileBase64, request.Config);

        return new PaddleocrPreviewResult
        {
            Texts = ocrResult.Texts,
            Images = ocrResult.Images
        };
    }
}