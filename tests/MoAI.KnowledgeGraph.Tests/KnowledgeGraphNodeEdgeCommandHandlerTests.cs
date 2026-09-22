using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;
using MoAI.KnowledgeGraph.Commands;
using MoAI.KnowledgeGraph.Consumers.Events;
using MoAI.KnowledgeGraph.Handlers;
using MoAI.KnowledgeGraph.Queries;
using MoAI.KnowledgeGraph.Services;
using Moq;
using Xunit;

namespace MoAI.KnowledgeGraph.Tests;

public class KnowledgeGraphNodeEdgeCommandHandlerTests
{
    private const long KnowledgeGraphId = 7;

    [Fact]
    public async Task CreateNode_WithEntityTypeNotInGraph_Throws400()
    {
        using var db = TestSqliteContext.Create();
        var authorizer = CreateAuthorizer();
        var store = new Mock<IKnowledgeGraphStore>();
        var sut = new CreateKnowledgeGraphNodeCommandHandler(db.Context, authorizer.Object, store.Object, CreateMessagePublisher(), CreateLogger<CreateKnowledgeGraphNodeCommandHandler>());

        var ex = await Assert.ThrowsAsync<BusinessException>(() => sut.Handle(
            new CreateKnowledgeGraphNodeCommand { KnowledgeGraphId = KnowledgeGraphId, EntityTypeId = 999, Name = "节点" },
            CancellationToken.None));

        Assert.Equal(400, ex.StatusCode);
        store.Verify(x => x.CreateNodeAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateEdge_WithMissingEndpointNode_Throws400()
    {
        using var db = TestSqliteContext.Create();
        var relationType = new KnowledgeGraphRelationTypeEntity
        {
            KnowledgeGraphId = KnowledgeGraphId,
            Name = "维护",
            Color = string.Empty,
            Description = string.Empty,
            Sort = 0,
        };
        db.Context.KnowledgeGraphRelationTypes.Add(relationType);
        await db.Context.SaveChangesAsync(CancellationToken.None);

        var authorizer = CreateAuthorizer();
        var store = new Mock<IKnowledgeGraphStore>();
        store.Setup(x => x.GetNodeAsync(KnowledgeGraphId, "missing", It.IsAny<CancellationToken>()))
            .ReturnsAsync((KnowledgeGraphNodeRecord?)null);

        var sut = new CreateKnowledgeGraphEdgeCommandHandler(db.Context, authorizer.Object, store.Object);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => sut.Handle(
            new CreateKnowledgeGraphEdgeCommand
            {
                KnowledgeGraphId = KnowledgeGraphId,
                RelationTypeId = relationType.Id,
                SourceNodeId = "missing",
                TargetNodeId = "target",
            },
            CancellationToken.None));

        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task CreateEdge_WhenEndpointViolatesRelationConstraint_Throws400()
    {
        using var db = TestSqliteContext.Create();
        var people = new KnowledgeGraphEntityTypeEntity
        {
            Properties = "[]",
            KnowledgeGraphId = KnowledgeGraphId,
            Name = "人员",
            Color = string.Empty,
            Description = string.Empty,
            Sort = 0,
        };
        var service = new KnowledgeGraphEntityTypeEntity
        {
            Properties = "[]",
            KnowledgeGraphId = KnowledgeGraphId,
            Name = "服务",
            Color = string.Empty,
            Description = string.Empty,
            Sort = 1,
        };
        db.Context.KnowledgeGraphEntityTypes.Add(people);
        db.Context.KnowledgeGraphEntityTypes.Add(service);
        await db.Context.SaveChangesAsync(CancellationToken.None);

        var relationType = new KnowledgeGraphRelationTypeEntity
        {
            KnowledgeGraphId = KnowledgeGraphId,
            Name = "维护",
            Color = string.Empty,
            Description = string.Empty,
            SourceTypeId = people.Id,
            TargetTypeId = service.Id,
            Sort = 0,
        };
        db.Context.KnowledgeGraphRelationTypes.Add(relationType);
        await db.Context.SaveChangesAsync(CancellationToken.None);

        var authorizer = CreateAuthorizer();
        var store = new Mock<IKnowledgeGraphStore>();
        store.Setup(x => x.GetNodeAsync(KnowledgeGraphId, "source", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new KnowledgeGraphNodeRecord("source", KnowledgeGraphId, service.Id, "起点", string.Empty));
        store.Setup(x => x.GetNodeAsync(KnowledgeGraphId, "target", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new KnowledgeGraphNodeRecord("target", KnowledgeGraphId, service.Id, "终点", string.Empty));

        var sut = new CreateKnowledgeGraphEdgeCommandHandler(db.Context, authorizer.Object, store.Object);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => sut.Handle(
            new CreateKnowledgeGraphEdgeCommand
            {
                KnowledgeGraphId = KnowledgeGraphId,
                RelationTypeId = relationType.Id,
                SourceNodeId = "source",
                TargetNodeId = "target",
            },
            CancellationToken.None));

        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task CreateEdge_HappyPath_ReturnsEdgeId()
    {
        using var db = TestSqliteContext.Create();
        var people = new KnowledgeGraphEntityTypeEntity
        {
            Properties = "[]",
            KnowledgeGraphId = KnowledgeGraphId,
            Name = "人员",
            Color = string.Empty,
            Description = string.Empty,
            Sort = 0,
        };
        var service = new KnowledgeGraphEntityTypeEntity
        {
            Properties = "[]",
            KnowledgeGraphId = KnowledgeGraphId,
            Name = "服务",
            Color = string.Empty,
            Description = string.Empty,
            Sort = 1,
        };
        db.Context.KnowledgeGraphEntityTypes.Add(people);
        db.Context.KnowledgeGraphEntityTypes.Add(service);
        await db.Context.SaveChangesAsync(CancellationToken.None);

        var relationType = new KnowledgeGraphRelationTypeEntity
        {
            KnowledgeGraphId = KnowledgeGraphId,
            Name = "维护",
            Color = string.Empty,
            Description = string.Empty,
            SourceTypeId = people.Id,
            TargetTypeId = service.Id,
            Sort = 0,
        };
        db.Context.KnowledgeGraphRelationTypes.Add(relationType);
        await db.Context.SaveChangesAsync(CancellationToken.None);

        var authorizer = CreateAuthorizer();
        var store = new Mock<IKnowledgeGraphStore>();
        store.Setup(x => x.GetNodeAsync(KnowledgeGraphId, "source", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new KnowledgeGraphNodeRecord("source", KnowledgeGraphId, people.Id, "起点", string.Empty));
        store.Setup(x => x.GetNodeAsync(KnowledgeGraphId, "target", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new KnowledgeGraphNodeRecord("target", KnowledgeGraphId, service.Id, "终点", string.Empty));
        store.Setup(x => x.CreateEdgeAsync(KnowledgeGraphId, relationType.Id, "source", "target", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new KnowledgeGraphEdgeRecord("edge-1", KnowledgeGraphId, relationType.Id, "source", "target"));

        var sut = new CreateKnowledgeGraphEdgeCommandHandler(db.Context, authorizer.Object, store.Object);

        var result = await sut.Handle(
            new CreateKnowledgeGraphEdgeCommand
            {
                KnowledgeGraphId = KnowledgeGraphId,
                RelationTypeId = relationType.Id,
                SourceNodeId = "source",
                TargetNodeId = "target",
            },
            CancellationToken.None);

        Assert.Equal("edge-1", result.Value);
    }

    [Fact]
    public async Task DeleteNode_WhenStoreReturnsFalse_Throws404()
    {
        using var db = TestSqliteContext.Create();
        var authorizer = CreateAuthorizer();
        var store = new Mock<IKnowledgeGraphStore>();
        store.Setup(x => x.DeleteNodeAsync(KnowledgeGraphId, "missing", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var sut = new DeleteKnowledgeGraphNodeCommandHandler(authorizer.Object, store.Object, CreateMessagePublisher(), CreateLogger<DeleteKnowledgeGraphNodeCommandHandler>());

        var ex = await Assert.ThrowsAsync<BusinessException>(() => sut.Handle(
            new DeleteKnowledgeGraphNodeCommand { KnowledgeGraphId = KnowledgeGraphId, NodeId = "missing" },
            CancellationToken.None));

        Assert.Equal(404, ex.StatusCode);
    }

    [Fact]
    public async Task QueryNodes_ClampsPageSizeAndMapsItems()
    {
        using var db = TestSqliteContext.Create();
        var authorizer = CreateAuthorizer();
        var store = new Mock<IKnowledgeGraphStore>();
        store.Setup(x => x.ListNodesAsync(KnowledgeGraphId, null, null, 1, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync((
                new List<KnowledgeGraphNodeRecord>
                {
                    new("n1", KnowledgeGraphId, 3, "节点一", "描述一"),
                },
                42L));

        var sut = new QueryKnowledgeGraphNodesCommandHandler(authorizer.Object, store.Object);

        var response = await sut.Handle(
            new QueryKnowledgeGraphNodesCommand { KnowledgeGraphId = KnowledgeGraphId, PageNo = 0, PageSize = 500 },
            CancellationToken.None);

        Assert.Equal(42, response.Total);
        var item = Assert.Single(response.Items);
        Assert.Equal("n1", item.NodeId);
        Assert.Equal(3, item.EntityTypeId);
        Assert.Equal("节点一", item.Name);
        Assert.Equal("描述一", item.Description);
        store.Verify(x => x.ListNodesAsync(KnowledgeGraphId, null, null, 1, 100, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateNode_OnConnectedGraph_Throws409()
    {
        using var db = TestSqliteContext.Create();
        var authorizer = new Mock<IKnowledgeGraphAuthorizer>();
        authorizer.Setup(x => x.AuthorizeManagedAsync(KnowledgeGraphId, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BusinessException("外部接入图谱为只读.") { StatusCode = 409 });
        var store = new Mock<IKnowledgeGraphStore>();
        var sut = new CreateKnowledgeGraphNodeCommandHandler(db.Context, authorizer.Object, store.Object, CreateMessagePublisher(), CreateLogger<CreateKnowledgeGraphNodeCommandHandler>());

        var ex = await Assert.ThrowsAsync<BusinessException>(() => sut.Handle(
            new CreateKnowledgeGraphNodeCommand { KnowledgeGraphId = KnowledgeGraphId, EntityTypeId = 1, Name = "节点" },
            CancellationToken.None));

        Assert.Equal(409, ex.StatusCode);
        store.Verify(x => x.CreateNodeAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateNode_HappyPath_PublishesUpsertDelta()
    {
        using var db = TestSqliteContext.Create();
        db.Context.KnowledgeGraphEntityTypes.Add(new KnowledgeGraphEntityTypeEntity
        {
            KnowledgeGraphId = KnowledgeGraphId,
            Name = "人员",
            Color = string.Empty,
            Description = string.Empty,
            Properties = "[]",
            Sort = 0,
        });
        await db.Context.SaveChangesAsync(CancellationToken.None);

        var store = new Mock<IKnowledgeGraphStore>();
        store.Setup(x => x.CreateNodeAsync(KnowledgeGraphId, It.IsAny<long>(), "节点", string.Empty, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new KnowledgeGraphNodeRecord("n1", KnowledgeGraphId, 1, "节点", string.Empty));

        var (publisher, messages) = CreateCapturingPublisher();
        var sut = new CreateKnowledgeGraphNodeCommandHandler(db.Context, CreateAuthorizer().Object, store.Object, publisher.Object, CreateLogger<CreateKnowledgeGraphNodeCommandHandler>());

        var result = await sut.Handle(
            new CreateKnowledgeGraphNodeCommand { KnowledgeGraphId = KnowledgeGraphId, EntityTypeId = 1, Name = "节点" },
            CancellationToken.None);

        Assert.Equal("n1", result.Value);
        var message = Assert.Single(messages);
        Assert.Equal(KnowledgeGraphId, message.KgId);
        Assert.Equal(new[] { "n1" }, message.UpsertNodeIds);
        Assert.Empty(message.DeleteNodeIds);
    }

    [Fact]
    public async Task UpdateNode_HappyPath_PublishesUpsertDelta()
    {
        using var db = TestSqliteContext.Create();
        db.Context.KnowledgeGraphEntityTypes.Add(new KnowledgeGraphEntityTypeEntity
        {
            KnowledgeGraphId = KnowledgeGraphId,
            Name = "人员",
            Color = string.Empty,
            Description = string.Empty,
            Properties = "[]",
            Sort = 0,
        });
        await db.Context.SaveChangesAsync(CancellationToken.None);

        var store = new Mock<IKnowledgeGraphStore>();
        store.Setup(x => x.GetNodeAsync(KnowledgeGraphId, "n1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new KnowledgeGraphNodeRecord("n1", KnowledgeGraphId, 1, "旧名", string.Empty));

        var (publisher, messages) = CreateCapturingPublisher();
        var sut = new UpdateKnowledgeGraphNodeCommandHandler(db.Context, CreateAuthorizer().Object, store.Object, publisher.Object, CreateLogger<UpdateKnowledgeGraphNodeCommandHandler>());

        await sut.Handle(
            new UpdateKnowledgeGraphNodeCommand { KnowledgeGraphId = KnowledgeGraphId, NodeId = "n1", EntityTypeId = 1, Name = "新名" },
            CancellationToken.None);

        var message = Assert.Single(messages);
        Assert.Equal(KnowledgeGraphId, message.KgId);
        Assert.Equal(new[] { "n1" }, message.UpsertNodeIds);
        Assert.Empty(message.DeleteNodeIds);
    }

    [Fact]
    public async Task DeleteNode_HappyPath_PublishesDeleteDelta()
    {
        using var db = TestSqliteContext.Create();
        var store = new Mock<IKnowledgeGraphStore>();
        store.Setup(x => x.DeleteNodeAsync(KnowledgeGraphId, "n1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var (publisher, messages) = CreateCapturingPublisher();
        var sut = new DeleteKnowledgeGraphNodeCommandHandler(CreateAuthorizer().Object, store.Object, publisher.Object, CreateLogger<DeleteKnowledgeGraphNodeCommandHandler>());

        await sut.Handle(
            new DeleteKnowledgeGraphNodeCommand { KnowledgeGraphId = KnowledgeGraphId, NodeId = "n1" },
            CancellationToken.None);

        var message = Assert.Single(messages);
        Assert.Equal(KnowledgeGraphId, message.KgId);
        Assert.Empty(message.UpsertNodeIds);
        Assert.Equal(new[] { "n1" }, message.DeleteNodeIds);
    }

    private static Mock<IKnowledgeGraphAuthorizer> CreateAuthorizer()
    {
        var authorizer = new Mock<IKnowledgeGraphAuthorizer>();
        authorizer.Setup(x => x.AuthorizeAsync(It.IsAny<long>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new KnowledgeGraphEntity { Id = KnowledgeGraphId, TeamId = 1, Name = "图谱" }, MoAI.Database.Enums.TeamRole.Admin));
        authorizer.Setup(x => x.AuthorizeManagedAsync(It.IsAny<long>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new KnowledgeGraphEntity { Id = KnowledgeGraphId, TeamId = 1, Name = "图谱" }, MoAI.Database.Enums.TeamRole.Admin));
        return authorizer;
    }

    private static Maomi.MQ.IMessagePublisher CreateMessagePublisher()
        => Mock.Of<Maomi.MQ.IMessagePublisher>();

    private static (Mock<Maomi.MQ.IMessagePublisher> Publisher, List<KgNodeEmbeddingDeltaMessage> Messages) CreateCapturingPublisher()
    {
        var messages = new List<KgNodeEmbeddingDeltaMessage>();
        var publisher = new Mock<Maomi.MQ.IMessagePublisher>();
        publisher
            .Setup(x => x.AutoPublishAsync(Capture.In(messages), null, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return (publisher, messages);
    }

    private static Microsoft.Extensions.Logging.ILogger<T> CreateLogger<T>()
        => Microsoft.Extensions.Logging.Abstractions.NullLogger<T>.Instance;
}


