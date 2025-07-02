using MoAI.Login.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MoAI.Login.Models;

public class OAuthBindUserProfile
{
    public int OAuthId { get; set; } = default!;

    public OpenIdUserProfile Profile { get; set; } = default!;

    public string AccessToken { get; set; } = default!;
}
