using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace MoAI.Filters;

/// <summary>
/// 统计统一的路由前缀.
/// </summary>
public class ApiApplicationModelConvention : IApplicationModelConvention
{
    private readonly string _routePrefix;

    /// <summary>
    /// Initializes a new instance of the <see cref="ApiApplicationModelConvention"/> class.
    /// </summary>
    /// <param name="routePrefix"></param>
    public ApiApplicationModelConvention(string routePrefix)
    {
        _routePrefix = routePrefix;
    }

    /// <inheritdoc/>
    public void Apply(ApplicationModel application)
    {
        foreach (var selector in application.Controllers.SelectMany(c => c.Selectors))
        {
            // 添加统一路由前缀
            if (selector.AttributeRouteModel != null)
            {
                if (!string.IsNullOrEmpty(selector.AttributeRouteModel.Template) && !selector.AttributeRouteModel.Template.StartsWith("/api", StringComparison.CurrentCultureIgnoreCase))
                {
                    if (selector.AttributeRouteModel.Template.StartsWith('/'))
                    {
                        selector.AttributeRouteModel.Template = $"{_routePrefix}{selector.AttributeRouteModel.Template}";
                    }
                    else
                    {
                        selector.AttributeRouteModel.Template = $"{_routePrefix}/{selector.AttributeRouteModel.Template}";
                    }
                }
            }

            // 如果不是免登录的接口
            if (selector.EndpointMetadata.Any(x => x.GetType() == typeof(AllowAnonymousAttribute)))
            {
                continue;
            }

            if (selector.EndpointMetadata.All(x => x.GetType() != typeof(AuthorizeAttribute)))
            {
                // 添加授权特性
                selector.EndpointMetadata.Add(new AuthorizeAttribute());
            }
        }
    }
}