// <copyright file="FastEndpointModule.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using FastEndpoints;
using FastEndpoints.Swagger;
using Maomi;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using MoAI.Infra;
using MoAI.Swaggers;
using Newtonsoft.Json.Serialization;
using NJsonSchema;
using NJsonSchema.Generation.TypeMappers;
using NSwag;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace MoAI.Modules;

/// <summary>
/// 使用 FastEndpoints 作为 API 的模块.
/// </summary>
public class FastEndpointModule : IModule
{
    /// <inheritdoc/>
    public void ConfigureServices(ServiceContext context)
    {
        context.Services
            .AddFastEndpoints(options =>
            {
                options.Assemblies = context.Modules.Select(x => x.Assembly).Distinct();
                options.IncludeAbstractValidators = true;
            })
            .SwaggerDocument(options =>
            {
                var settings = options.Services.GetRequiredService<SystemOptions>();
                var serverAddressesFeature = options.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>();

                options.ShortSchemaNames = true;

                options.DocumentSettings = s =>
                {
                    s.Title = "AI API";
                    s.Version = "v1";
                    s.Description = "MoAI openapi document.";

                    // s.AddAuth("Bearer", new()
                    // {
                    //    Type = OpenApiSecuritySchemeType.Http,
                    //    Scheme = JwtBearerDefaults.AuthenticationScheme,
                    //    BearerFormat = "JWT",
                    // });
                    s.MarkNonNullablePropsAsRequired();
                    s.PostProcess = d =>
                    {
                        d.Servers.Add(new OpenApiServer
                        {
                            Url = settings.Server,
                            Description = "User-defined service address"
                        });

                        if (serverAddressesFeature != null)
                        {
                            foreach (var address in serverAddressesFeature.Addresses)
                            {
                                d.Servers.Add(new OpenApiServer
                                {
                                    Url = address,
                                    Description = "Local service address"
                                });
                            }
                        }
                    };

                    // 要与 SerializerSettings 对应序列化配置和显示的 Swagger 类型
                    // todo: 后续添加，https://maomi.whuanle.cn/10.web.html#%E6%A8%A1%E5%9E%8B%E7%B1%BB%E5%B1%9E%E6%80%A7%E7%B1%BB%E5%9E%8B%E5%A4%84%E7%90%86
                    s.SchemaSettings.TypeMappers.Add(
                        new PrimitiveTypeMapper(
                            typeof(Guid),
                            schema =>
                                {
                                    schema.Type = JsonObjectType.String;
                                    schema.Format = "uuid";
                                }));

                    s.SchemaSettings.TypeMappers.Add(new LongTypeMapper());
                    s.SchemaSettings.TypeMappers.Add(new DateTimeOffsetTypeMapper());
                    s.OperationProcessors.Add(new ErrorResponseOperationProcessor());
                };

                options.SerializerSettings = s =>
                {
                    s.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                    s.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
                    s.PropertyNameCaseInsensitive = true;
                    s.NumberHandling = JsonNumberHandling.AllowReadingFromString;

                    // 这里配置的转换器不会起效，只能用于生成文档，需要 UseFastEndpoints 配置请求和响应序列化
                    // https://maomi.whuanle.cn/10.web.html#%E6%A8%A1%E5%9E%8B%E7%B1%BB%E5%B1%9E%E6%80%A7%E7%B1%BB%E5%9E%8B%E5%A4%84%E7%90%86
                    s.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));

                    // s.Converters.Add(new DateTimeOffsetConverter());
                    s.Converters.Add(JsonMetadataServices.DecimalConverter);
                };

                // NSWAG 只能使用 Newtonsoft
                options.NewtonsoftSettings = s =>
                {
                    s.Converters.Add(new Newtonsoft.Json.Converters.StringEnumConverter() { NamingStrategy = new CamelCaseNamingStrategy() });
                };
            });
    }
}