#pragma warning disable CA1031 // 不捕获常规异常类型
#pragma warning disable SA1118 // Parameter should not span multiple lines

using Maomi;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Infra.Paddleocr;
using MoAI.Infra.Paddleocr.Models;
using MoAI.Infra.Put;
using MoAI.Infra.System.Text.Json;
using MoAI.Plugin.Attributes;
using MoAI.Plugin.Models;
using MoAI.Plugin.Paddleocr.Ocr;
using MoAI.Plugin.Plugins.Paddleocr.Common;
using MoAI.Storage.Commands;
using MoAI.Storage.Helpers;
using MoAI.Store.Queries;
using System.ComponentModel;
using System.Text;
using System.Text.Json;

namespace MoAI.Plugin.Paddleocr.Common;

public class PaddleocrPluginBase
{
    protected readonly IMediator _mediator;
    private readonly IServiceProvider _serviceProvider;

    public PaddleocrPluginBase(IServiceProvider serviceProvider, IMediator mediator)
    {
        _serviceProvider = serviceProvider;
        _mediator = mediator;
    }

    protected async Task UploadTempFileAsync(List<string> images, string imageUrl, CancellationToken cancellationToken = default)
    {
        var tempFilePath = Path.GetTempFileName();
        try
        {
            var putClient = _serviceProvider.GetRequiredService<IPutClient>();

            // 下载文件
            using var fileStream = File.Create(tempFilePath);
            var uri = new Uri(imageUrl);

            using var httpStream = await putClient.Client.GetStreamAsync(uri);
            await httpStream.CopyToAsync(fileStream, cancellationToken);
            await fileStream.FlushAsync();

            fileStream.Seek(0, SeekOrigin.Begin);

            var md5 = FileStoreHelper.CalculateFileMd5(fileStream);
            fileStream.Seek(0, SeekOrigin.Begin);
            var fileName = $"{Guid.CreateVersion7()}.jpg";
            var objectKey = FileStoreHelper.GetObjectKey(md5: md5, fileName: fileName, prefix: "paddleocr/ocr");

            await _mediator.Send(new UploadFileStreamCommand
            {
                ObjectKey = objectKey,
                MD5 = md5,
                ContentType = "image/jpg",
                FileStream = fileStream,
                FileSize = (int)fileStream.Length
            });

            var downloadUrlResponse = await _mediator.Send(new QueryFileDownloadUrlCommand
            {
                ExpiryDuration = TimeSpan.FromHours(2),
                ObjectKeys = new KeyValueString[]
                {
                                new KeyValueString
                                {
                                    Key = objectKey,
                                    Value = fileName
                                }
                }
            });

            var url = downloadUrlResponse.Urls.FirstOrDefault().Value?.ToString();
            if (!string.IsNullOrWhiteSpace(url))
            {
                images.Add(url);
            }
        }
        catch (Exception ex)
        {

        }
        finally
        {
            if (File.Exists(tempFilePath))
            {
                File.Delete(tempFilePath);
            }
        }
    }
}
