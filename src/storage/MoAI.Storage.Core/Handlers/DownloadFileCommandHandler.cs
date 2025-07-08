// <copyright file="DownloadFileCommandHandler.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using MoAI.Infra;
using MoAI.Infra.Models;
using MoAI.Storage.Commands;

namespace MoAI.Storage.Handlers;

public class DownloadFileCommandHandler : IRequestHandler<DownloadFileCommand, EmptyCommandResponse>
{
    private readonly SystemOptions _systemOptions;

    public DownloadFileCommandHandler(SystemOptions systemOptions)
    {
        _systemOptions = systemOptions;
    }

    public async Task<EmptyCommandResponse> Handle(DownloadFileCommand request, CancellationToken cancellationToken)
    {
        var visibility = request.Visibility.ToString().ToLower();

        var sourceFilePath = Path.Combine(_systemOptions.FilePath, visibility, request.ObjectKey);
        if (!File.Exists(sourceFilePath))
        {
            throw new FileNotFoundException($"文件 {request.ObjectKey} 不存在于 {_systemOptions.FilePath} 路径下。");
        }

        File.Copy(sourceFilePath, request.StoreFilePath, true);

        return EmptyCommandResponse.Default;
    }
}