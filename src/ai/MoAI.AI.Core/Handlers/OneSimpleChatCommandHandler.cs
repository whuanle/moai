using MediatR;
using MoAI.AI.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MoAI.AI.Handlers;

public class OneSimpleChatCommandHandler : IRequestHandler<OneSimpleChatCommand, OneSimpleChatCommandResponse>
{
    public Task<OneSimpleChatCommandResponse> Handle(OneSimpleChatCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
