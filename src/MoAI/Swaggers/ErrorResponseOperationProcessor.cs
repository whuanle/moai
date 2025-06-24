// <copyright file="ErrorResponseOperationProcessor.cs" company="MaomiAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MoAI.Infra.Models;
using NSwag;
using NSwag.Generation.Processors;
using NSwag.Generation.Processors.Contexts;

namespace MoAI.Swaggers;

/// <summary>
/// Swagger 统一响应处理.
/// </summary>
public class ErrorResponseOperationProcessor : IOperationProcessor
{
    /// <inheritdoc/>
    public bool Process(OperationProcessorContext context)
    {
        var schemaReference = context.SchemaResolver.HasSchema(typeof(BusinessValidationResult), false);
        NJsonSchema.JsonSchema? responseSchema;

        if (schemaReference != false)
        {
            responseSchema = context.SchemaResolver.GetSchema(typeof(BusinessValidationResult), false);
        }
        else
        {
            responseSchema = context.SchemaGenerator.Generate(typeof(BusinessValidationResult), context.SchemaResolver);
        }

        foreach (var statusCode in new[] { "400", "401", "403", "409", "500" })
        {
            responseSchema = context.SchemaGenerator.Generate(typeof(BusinessValidationResult), context.SchemaResolver);
            var response = new OpenApiResponse
            {
                Description = "An error occurred in the request.",
                Content =
                {
                    ["application/json"] = new OpenApiMediaType { Schema = responseSchema }
                }
            };

            context.OperationDescription.Operation.Responses[statusCode] = response;
        }

        return true;
    }
}
