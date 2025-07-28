// <copyright file="OneSimpleChatCommandHandler.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using MoAI.AI.Commands;

namespace MoAI.AI.Handlers;

public class OneSimpleChatCommandHandler : IRequestHandler<OneSimpleChatCommand, OneSimpleChatCommandResponse>
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="OneSimpleChatCommandHandler"/> class.
    /// </summary>
    /// <param name="serviceProvider"></param>
    public OneSimpleChatCommandHandler(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task<OneSimpleChatCommandResponse> Handle(OneSimpleChatCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
