// <copyright file="UpdateWebDocumentConfigCommandValidators.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using FastEndpoints;
using FluentValidation;
using MoAI.Wiki.WebDocuments.Commands;

namespace MoAI.Wiki.WebDocuments.Validators;

/// <summary>
/// UpdateWebDocumentConfigCommandValidators.
/// </summary>
public class UpdateWebDocumentConfigCommandValidators : Validator<UpdateWebDocumentConfigCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateWebDocumentConfigCommandValidators"/> class.
    /// </summary>
    public UpdateWebDocumentConfigCommandValidators()
    {
        RuleFor(x => x.WikiId)
            .NotEmpty().WithMessage("知识库id不正确")
            .GreaterThan(0).WithMessage("知识库id不正确");

        RuleFor(x => x.WebConfigId)
            .NotEmpty().WithMessage("爬虫配置id不正确")
            .GreaterThan(0).WithMessage("爬虫配置id不正确");

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("配置名称不能为空")
            .MaximumLength(50).WithMessage("配置名称不能超过50个字符");

        RuleFor(x => x.LimitMaxCount)
            .GreaterThan(0).WithMessage("最大抓取数量必须大于0")
            .LessThanOrEqualTo(1000).WithMessage("最大抓取数量不能超过1000");

        RuleFor(x => x.Selector)
            .MaximumLength(255).WithMessage("页面选择器规则不能超过255字符");
    }
}
