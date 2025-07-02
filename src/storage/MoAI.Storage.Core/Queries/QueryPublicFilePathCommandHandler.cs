//// <copyright file="QueryPublicFilePathCommandHandler.cs" company="MoAI">
//// Copyright (c) MoAI. All rights reserved.
//// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//// Github link: https://github.com/whuanle/moai
//// </copyright>

//using MediatR;
//using Microsoft.EntityFrameworkCore;
//using MoAI.Database;
//using MoAI.Infra;
//using MoAI.Storage.Queries;
//using MoAI.Storage.Queries.Response;

//namespace MoAI.Store.Queries;

///// <summary>
///// 获取文件路径.
///// </summary>
//public class QueryPublicFilePathCommandHandler : IRequestHandler<QueryPublicFilePathCommand, QueryPublicFilePathResponse>
//{
//    private readonly DatabaseContext _dbContext;
//    private readonly IServiceProvider _serviceProvider;
//    private readonly SystemOptions _systemOptions;

//    /// <summary>
//    /// Initializes a new instance of the <see cref="QueryPublicFilePathCommandHandler"/> class.
//    /// </summary>
//    /// <param name="dbContext"></param>
//    /// <param name="serviceProvider"></param>
//    /// <param name="systemOptions"></param>
//    public QueryPublicFilePathCommandHandler(DatabaseContext dbContext, IServiceProvider serviceProvider, SystemOptions systemOptions)
//    {
//        _dbContext = dbContext;
//        _serviceProvider = serviceProvider;
//        _systemOptions = systemOptions;
//    }

//    /// <inheritdoc/>
//    public async Task<QueryPublicFilePathResponse> Handle(QueryPublicFilePathCommand request, CancellationToken cancellationToken)
//    {
//        var query = _dbContext.Files.Where(x => x.IsPublic == true);
//        if (request.FileId != null)
//        {
//            query = query.Where(x => x.Id == request.FileId);
//        }

//        if (!string.IsNullOrEmpty(request.MD5))
//        {
//            query = query.Where(x => x.FileMd5 == request.MD5);
//        }

//        if (!string.IsNullOrEmpty(request.Key))
//        {
//            query = query.Where(x => x.ObjectKey == request.Key);
//        }

//        var existFile = await query.FirstOrDefaultAsync(cancellationToken);
//        if (existFile == null)
//        {
//            return new QueryPublicFilePathResponse
//            {
//                Exist = false
//            };
//        }

//        var url = Path.Combine(_systemOptions.Storage.S3Public.Endpoint, existFile.ObjectKey);

//        return new QueryPublicFilePathResponse
//        {
//            Exist = true,
//            Path = existFile.ObjectKey,
//            Url = url,
//        };
//    }
//}