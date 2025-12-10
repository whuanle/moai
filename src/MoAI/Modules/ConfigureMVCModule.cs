using FluentValidation;
using Maomi;
using Microsoft.AspNetCore.Mvc;
using MoAI.Filters;
using MoAI.Infra;
using MoAI.Storage;
using SharpGrip.FluentValidation.AutoValidation.Mvc.Enums;
using SharpGrip.FluentValidation.AutoValidation.Mvc.Extensions;
using SharpGrip.FluentValidation.AutoValidation.Mvc.Interceptors;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace MoAI.Modules;

/// <summary>
/// 配置 MVC .
/// </summary>
public class ConfigureMVCModule : IModule
{
    private readonly IConfiguration _configuration;
    private readonly SystemOptions _systemOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigureMVCModule"/> class.
    /// </summary>
    /// <param name="configuration"></param>
    public ConfigureMVCModule(IConfiguration configuration)
    {
        _configuration = configuration;
        _systemOptions = configuration.Get<SystemOptions>()!;
    }

    /// <inheritdoc/>
    public void ConfigureServices(ServiceContext context)
    {
        context.Services.Configure<ApiBehaviorOptions>(options =>
        {
            // 禁用默认的模型验证器.
            options.SuppressModelStateInvalidFilter = true;
        });

        var mvcBuilder = context.Services.AddControllers(o =>
        {
            o.Filters.Add<MaomiExceptionFilter>();
            o.Conventions.Add(new ApiApplicationModelConvention("/api"));
        }).AddJsonOptions(options =>
        {
            var jsonOptions = options.JsonSerializerOptions;
            jsonOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            jsonOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
            jsonOptions.PropertyNameCaseInsensitive = true;
            jsonOptions.NumberHandling = JsonNumberHandling.AllowReadingFromString;

            jsonOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));

            // jsonOptions.Converters.Add(item: new DateTimeOffsetConverter());
            jsonOptions.Converters.Add(JsonMetadataServices.DecimalConverter);
        });

        Validation(context);

        AddApplicationParts(mvcBuilder, context);

        context.Services.AddCors(options =>
        {
            options.AddPolicy(
                name: "AllowSpecificOrigins",
                policy =>
                {
                    policy.AllowAnyOrigin()
                    .AllowAnyHeader()
                    .AllowAnyMethod();
                });
        });

        context.Services.AddValidatorsFromAssemblies(context.Modules.Select(x => x.Assembly).Distinct());
    }

    private static void Validation(ServiceContext context)
    {
        // 模型验证
        context.Services.AddFluentValidationAutoValidation(configuration =>
        {
            // 禁用 ASP.NET Core 的模型验证.
            configuration.DisableBuiltInModelValidation = true;
            configuration.OverrideDefaultResultFactoryWith<CustomResultFactory>();
        });

        ValidatorOptions.Global.PropertyNameResolver = (type, memberInfo, expression) =>
        {
            var name = memberInfo?.Name;
            if (string.IsNullOrEmpty(name))
            {
                return name;
            }

            return char.ToLowerInvariant(name[0]) + (name.Length > 1 ? name.Substring(1) : string.Empty);
        };

        //context.Services.AddTransient<IGlobalValidationInterceptor, LowerCaseValidationInterceptor>();
    }

    private static void AddApplicationParts(IMvcBuilder builder, ServiceContext context)
    {
        foreach (var item in context.Modules)
        {
            if (item.Assembly.GetName().Name?.EndsWith(".Api", StringComparison.CurrentCultureIgnoreCase) == true)
            {
                builder.AddApplicationPart(item.Assembly);
            }
        }

        builder.AddApplicationPart(typeof(StorageLocalModule).Assembly);
    }
}
