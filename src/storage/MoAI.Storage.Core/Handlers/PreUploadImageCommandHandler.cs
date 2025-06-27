// <copyright file="PreUploadImageCommandHandler.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using MoAI.Infra.Exceptions;
using MoAI.Storage.Commands;
using MoAI.Storage.Commands.Response;
using MoAI.Storage.Helpers;
using MoAI.Store.Enums;

namespace MoAI.Storage.Handlers;

/// <summary>
/// 预上传图片，存储时设置为公开.
/// </summary>
public class PreUploadImageCommandHandler : IRequestHandler<PreUploadImageCommand, PreUploadFileCommandResponse>
{
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="PreUploadImageCommandHandler"/> class.
    /// </summary>
    /// <param name="mediator"></param>
    public PreUploadImageCommandHandler(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public async Task<PreUploadFileCommandResponse> Handle(PreUploadImageCommand request, CancellationToken cancellationToken)
    {
        var fileExtension = Path.GetExtension(request.FileName);
        if (!FileStoreHelper.ImageExtensions.Contains(fileExtension.ToLower()))
        {
            throw new BusinessException("不支持该类型的图像") { StatusCode = 400 };
        }

        // todo: 通过系统设置限制头像文件大小.

        var preu = new PreUploadFileCommand
        {
            FileName = request.FileName,
            ContentType = request.ContentType,
            FileSize = request.FileSize,
            MD5 = request.MD5,
            Expiration = TimeSpan.FromMinutes(1),
            Visibility = FileVisibility.Public,
            ObjectKey = FileStoreHelper.GetObjectKey(request.MD5, request.FileName),
        };

        return await _mediator.Send(preu, cancellationToken);
    }
}
