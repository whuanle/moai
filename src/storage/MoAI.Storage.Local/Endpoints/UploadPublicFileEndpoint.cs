// <copyright file="UploadPublicFileEndpoint.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>
// namespace MoAI.Storage.Endpoints;


///// <summary>
///// 兼容 S3 接口的文件上传端点.
///// </summary>
// [AllowFileUploads]
// [FastEndpoints.HttpPut($"{ApiPrefix.Prefix}/public/upload/{"{objectKey}"}")]
// public class UploadPublicFileEndpoint : Endpoint<AAA, string>
// {
//    public override async Task<string> HandleAsync(AAA aaa, CancellationToken ct)
//    {
//        await foreach (var section in FormFileSectionsAsync(ct))
//        {
//            if (section is not null)
//            {
//                using (var fs = System.IO.File.Create(section.FileName))
//                {
//                    await section.Section.Body.CopyToAsync(fs, 1024 * 64, ct);
//                }
//            }
//        }

// return "upload complete!";
//    }
// }


// public class AAA
// {
//    public string ObjectKey { get; set; }
// }
