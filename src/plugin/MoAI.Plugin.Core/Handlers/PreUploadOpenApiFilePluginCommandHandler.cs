// <copyright file="PreUploadOpenApiFilePluginCommandHandler.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MaomiAI.Plugin.Shared.Commands.Responses;
using MediatR;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Plugin.Commands;
using MoAI.Storage.Commands;
using MoAI.Storage.Helpers;
using MoAI.Store.Enums;

namespace MaomiAI.Plugin.Core.Commands;

/// <summary>
/// 预上传 openapi 文件，支持 json、yaml.
/// </summary>
public class PreUploadOpenApiFilePluginCommandHandler : IRequestHandler<PreUploadOpenApiFilePluginCommand, PreUploadOpenApiFilePluginCommandResponse>
{
    private readonly IMediator _mediator;
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="PreUploadOpenApiFilePluginCommandHandler"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    /// <param name="databaseContext"></param>
    public PreUploadOpenApiFilePluginCommandHandler(IMediator mediator, DatabaseContext databaseContext)
    {
        _mediator = mediator;
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<PreUploadOpenApiFilePluginCommandResponse> Handle(PreUploadOpenApiFilePluginCommand request, CancellationToken cancellationToken)
    {
        if (!FileStoreHelper.OpenApiFormats.Contains(Path.GetExtension(request.FileName).ToLower()))
        {
            throw new BusinessException("不支持的文件格式，请导入 .json/.yaml/.yml 文件") { StatusCode = 400 };
        }

        // 检查文件.
        var objectKey = FileStoreHelper.GetObjectKey(md5: request.MD5, fileName: request.FileName, prefix: "plugin");

        var result = await _mediator.Send(new PreUploadFileCommand
        {
            MD5 = request.MD5,
            FileName = request.FileName,
            ContentType = request.ContentType,
            FileSize = request.FileSize,
            Visibility = FileVisibility.Private,
            ObjectKey = objectKey,
            Expiration = TimeSpan.FromMinutes(2),
        });

        if (result.IsExist)
        {
            return new PreUploadOpenApiFilePluginCommandResponse
            {
                FileId = result.FileId,
                IsExist = true,
            };
        }

        return new PreUploadOpenApiFilePluginCommandResponse
        {
            Visibility = FileVisibility.Private,
            FileId = result.FileId,
            IsExist = result.IsExist,
            UploadUrl = result.UploadUrl,
            Expiration = result.Expiration
        };
    }
}