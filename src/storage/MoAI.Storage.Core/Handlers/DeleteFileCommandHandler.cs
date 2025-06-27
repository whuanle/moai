// <copyright file="DeleteFileCommandHandler.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using MoAI.Infra.Models;
using MoAI.Storage.Commands;

namespace MoAI.Storage.Handlers;

public class DeleteFileCommandHandler : IRequestHandler<DeleteFileCommand, EmptyCommandResponse>
{
    public async Task<EmptyCommandResponse> Handle(DeleteFileCommand request, CancellationToken cancellationToken)
    {
        return EmptyCommandResponse.Default;
    }
}