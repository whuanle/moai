using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MoAI.AIChannel.Services;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Defaults;
using MoAI.Infra.Services;
using MoAI.Wiki.Services;
using Moq;
using Xunit;

namespace MoAI.UsageCounters.Tests;

public class WikiEmbeddingServiceTests
{
    private static readonly Guid EmbeddingModelId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ChannelId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public void ProcessAsync_CompileContract_DoesNotAcceptMetadataModelId()
    {
        Func<WikiEmbeddingService, int, int, bool, bool, CancellationToken, Task> call =
            static (service, wikiId, documentId, isEmbedSourceText, isEmbedMetadata, cancellationToken) =>
                service.ProcessAsync(wikiId, documentId, isEmbedSourceText, isEmbedMetadata, cancellationToken);

        Assert.NotNull(call);
    }

    [Fact]
    public async Task ProcessAsync_WhenMetadataOnlyProducesNoRecords_ThrowsReadableException()
    {
        using var db = CreateContext();
        await SeedValidEmbeddingContextAsync(db.Context, includeMetadata: false);

        var provider = CreateGeneratorProvider(CreateGeneratorWithSingleVector());
        var sut = CreateSut(db.Context, provider.Object);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.ProcessAsync(101, 202, isEmbedSourceText: false, isEmbedMetadata: true, CancellationToken.None));

        Assert.Contains("未构建出任何可向量化记录", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProcessAsync_WhenGeneratorReturnsNoVectors_ThrowsWithoutTouchingVectorStore()
    {
        using var db = CreateContext();
        await SeedValidEmbeddingContextAsync(db.Context, includeMetadata: false);

        var generator = new Mock<IEmbeddingGenerator<string, Embedding<float>>>();
        generator
            .Setup(x => x.GenerateAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<EmbeddingGenerationOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GeneratedEmbeddings<Embedding<float>>());

        var provider = CreateGeneratorProvider(generator.Object);
        var store = new Mock<IWikiEmbeddingVectorStore>(MockBehavior.Strict);
        var sut = CreateSut(db.Context, provider.Object, store.Object);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.ProcessAsync(101, 202, isEmbedSourceText: true, isEmbedMetadata: false, CancellationToken.None));

        Assert.Contains("向量模型未返回向量", ex.Message, StringComparison.Ordinal);
        store.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ProcessAsync_WhenGeneratorReturnsEmptyVector_ThrowsWithoutTouchingVectorStore()
    {
        using var db = CreateContext();
        await SeedValidEmbeddingContextAsync(db.Context, includeMetadata: false);

        var emptyVector = new Embedding<float>(Array.Empty<float>());
        var generator = new Mock<IEmbeddingGenerator<string, Embedding<float>>>();
        generator
            .Setup(x => x.GenerateAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<EmbeddingGenerationOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GeneratedEmbeddings<Embedding<float>>([emptyVector]));

        var provider = CreateGeneratorProvider(generator.Object);
        var store = new Mock<IWikiEmbeddingVectorStore>(MockBehavior.Strict);
        var sut = CreateSut(db.Context, provider.Object, store.Object);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.ProcessAsync(101, 202, isEmbedSourceText: true, isEmbedMetadata: false, CancellationToken.None));

        Assert.Contains("向量模型返回空向量", ex.Message, StringComparison.Ordinal);
        store.VerifyNoOtherCalls();
    }

    [Fact]
    public void BuildEmbeddingRecords_WhenSourceOnly_CreatesSourceRecordPerChunk()
    {
        var chunks = CreateChunks();
        var metadataByChunk = CreateMetadata(chunks);

        var records = WikiEmbeddingService.BuildEmbeddingRecords(
            wikiId: 1,
            documentId: 2,
            chunks,
            metadataByChunk,
            isEmbedSourceText: true,
            isEmbedMetadata: false);

        Assert.Equal(chunks.Count, records.Count);
        Assert.All(records, item => Assert.Equal(0, item.MetadataType));
        Assert.Equal(chunks.Select(x => x.Id).OrderBy(x => x), records.Select(x => x.ChunkId).OrderBy(x => x));
        Assert.All(records, item => Assert.NotEqual(Guid.Empty, item.Key));
    }

    [Fact]
    public void BuildEmbeddingRecords_WhenMetadataOnly_UsesExistingMetadataWithoutSource()
    {
        var chunks = CreateChunks();
        var metadataByChunk = CreateMetadata(chunks);

        var records = WikiEmbeddingService.BuildEmbeddingRecords(
            wikiId: 1,
            documentId: 2,
            chunks,
            metadataByChunk,
            isEmbedSourceText: false,
            isEmbedMetadata: true);

        var expectedMetadataCount = metadataByChunk.Values.Sum(x => x.Count);
        Assert.Equal(expectedMetadataCount, records.Count);
        Assert.All(records, item => Assert.NotEqual(0, item.MetadataType));
        Assert.All(records, item => Assert.Contains(item.ChunkId, chunks.Select(x => x.Id)));
    }

    [Fact]
    public void BuildEmbeddingRecords_WhenBothEnabled_MetadataPointsToSameChunk()
    {
        var chunks = CreateChunks();
        var metadataByChunk = CreateMetadata(chunks);

        var records = WikiEmbeddingService.BuildEmbeddingRecords(
            wikiId: 1,
            documentId: 2,
            chunks,
            metadataByChunk,
            isEmbedSourceText: true,
            isEmbedMetadata: true);

        var sourceChunkIds = records
            .Where(x => x.MetadataType == 0)
            .Select(x => x.ChunkId)
            .ToHashSet();

        var metadataRecords = records.Where(x => x.MetadataType != 0).ToList();
        Assert.NotEmpty(metadataRecords);
        Assert.All(metadataRecords, item => Assert.Contains(item.ChunkId, sourceChunkIds));
    }

    [Fact]
    public void BuildEmbeddingRecords_WhenBothFlagsFalse_ThrowsArgumentException()
    {
        var chunks = CreateChunks();

        var ex = Assert.Throws<ArgumentException>(() => WikiEmbeddingService.BuildEmbeddingRecords(
            wikiId: 1,
            documentId: 2,
            chunks,
            new Dictionary<long, List<WikiDocumentChunkMetadatumEntity>>(),
            isEmbedSourceText: false,
            isEmbedMetadata: false));

        Assert.Contains("至少需要选择", ex.Message, StringComparison.Ordinal);
    }

    private static List<WikiDocumentChunkContentEntity> CreateChunks()
    {
        return new List<WikiDocumentChunkContentEntity>
        {
            new() { Id = 101, WikiId = 1, DocumentId = 2, SliceOrder = 1, SliceLength = 5, SliceContent = "chunk-1" },
            new() { Id = 102, WikiId = 1, DocumentId = 2, SliceOrder = 2, SliceLength = 5, SliceContent = "chunk-2" },
        };
    }

    private static Dictionary<long, List<WikiDocumentChunkMetadatumEntity>> CreateMetadata(List<WikiDocumentChunkContentEntity> chunks)
    {
        return new Dictionary<long, List<WikiDocumentChunkMetadatumEntity>>
        {
            [chunks[0].Id] = new List<WikiDocumentChunkMetadatumEntity>
            {
                new() { Id = 1, WikiId = 1, DocumentId = 2, ChunkId = chunks[0].Id, MetadataType = 1, MetadataContent = "m1" },
                new() { Id = 2, WikiId = 1, DocumentId = 2, ChunkId = chunks[0].Id, MetadataType = 2, MetadataContent = "m2" },
            },
            [chunks[1].Id] = new List<WikiDocumentChunkMetadatumEntity>
            {
                new() { Id = 3, WikiId = 1, DocumentId = 2, ChunkId = chunks[1].Id, MetadataType = 4, MetadataContent = "m3" },
            },
        };
    }

    private static WikiEmbeddingService CreateSut(DatabaseContext context, IEmbeddingGeneratorProvider embeddingGeneratorProvider, IWikiEmbeddingVectorStore? vectorStore = null)
    {
        return new WikiEmbeddingService(
            context,
            embeddingGeneratorProvider,
            Mock.Of<IAiChatCompletionService>(),
            vectorStore ?? Mock.Of<IWikiEmbeddingVectorStore>(),
            CreateIdProvider(),
            NullLogger<WikiEmbeddingService>.Instance);
    }

    private static Mock<IEmbeddingGeneratorProvider> CreateGeneratorProvider(IEmbeddingGenerator<string, Embedding<float>> generator)
    {
        var provider = new Mock<IEmbeddingGeneratorProvider>();
        provider
            .Setup(x => x.GetEmbeddingGeneratorAsync(It.IsAny<AiModelEntity>(), It.IsAny<AiChannelEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(generator);
        return provider;
    }

    private static IEmbeddingGenerator<string, Embedding<float>> CreateGeneratorWithSingleVector()
    {
        var generator = new Mock<IEmbeddingGenerator<string, Embedding<float>>>();
        generator
            .Setup(x => x.GenerateAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<EmbeddingGenerationOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GeneratedEmbeddings<Embedding<float>>([new Embedding<float>(new[] { 0.1f, 0.2f })]));
        return generator.Object;
    }

    private static MoAI.Infra.Services.IIdProvider CreateIdProvider()
    {
        var provider = new Mock<MoAI.Infra.Services.IIdProvider>();
        provider.Setup(x => x.NextId()).Returns(1);
        provider.Setup(x => x.GeneratorId(out It.Ref<string>.IsAny)).Returns(1);
        provider.Setup(x => x.GeneratorKey()).Returns("k");
        return provider.Object;
    }

    private static IUserContextProvider CreateUserContextProvider()
    {
        var provider = new Mock<IUserContextProvider>();
        provider.Setup(x => x.GetUserContext()).Returns(new DefaultUserContext
        {
            IsAuthenticated = true,
            UserId = 1001,
            UserName = "tester",
            NickName = "tester",
            Email = "tester@example.com",
        });
        return provider.Object;
    }

    private static SqliteContextScope CreateContext(SqliteConnection? sharedConnection = null)
    {
        var ownConnection = sharedConnection == null;
        var connection = sharedConnection ?? new SqliteConnection("Data Source=:memory:");
        if (connection.State != ConnectionState.Open)
        {
            connection.Open();
        }

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton(CreateIdProvider());
        serviceCollection.AddSingleton(CreateUserContextProvider());
        var serviceProvider = serviceCollection.BuildServiceProvider();

        var options = new DbContextOptionsBuilder<DatabaseContext>()
            .UseSqlite(connection)
            .Options;

        var context = new TestDatabaseContext(options, serviceProvider);
        context.Database.EnsureCreated();
        return new SqliteContextScope(context, connection, ownConnection);
    }

    private static async Task SeedValidEmbeddingContextAsync(DatabaseContext context, bool includeMetadata)
    {
        var now = DateTimeOffset.UtcNow;

        await context.AiChannels.AddAsync(new AiChannelEntity
        {
            Id = ChannelId,
            ProviderKey = "openai",
            Name = "OpenAI",
            ProtocolFamily = 1,
            BaseUrl = "https://example.com",
            ApiKey = "k",
            Enabled = true,
            CreateTime = now,
            UpdateTime = now,
            CreateUserId = 1,
            UpdateUserId = 1,
            IsDeleted = 0,
        });

        await context.AiModels.AddAsync(new AiModelEntity
        {
            Id = EmbeddingModelId,
            ChannelId = ChannelId,
            ModelId = "text-embedding-3-large",
            Name = "Embedding",
            ModelKind = "embedding",
            Enabled = true,
            IsPublic = true,
            CreateTime = now,
            UpdateTime = now,
            CreateUserId = 1,
            UpdateUserId = 1,
            IsDeleted = 0,
        });

        await context.Wikis.AddAsync(new WikiEntity
        {
            Id = 101,
            TeamId = 300,
            Name = "wiki",
            Description = "desc",
            AvatarPath = string.Empty,
            EmbeddingModelId = EmbeddingModelId,
            EmbeddingDimensions = 2,
            CreateTime = now,
            UpdateTime = now,
            CreateUserId = 1,
            UpdateUserId = 1,
            IsDeleted = 0,
        });

        await context.WikiDocuments.AddAsync(new WikiDocumentEntity
        {
            Id = 202,
            WikiId = 101,
            FileId = 1,
            ObjectKey = "test.md",
            FileName = "test.md",
            FileType = ".md",
            SliceConfig = "{}",
            CreateTime = now,
            UpdateTime = now,
            CreateUserId = 1,
            UpdateUserId = 1,
            IsDeleted = 0,
        });

        await context.WikiDocumentContents.AddAsync(new WikiDocumentContentEntity
        {
            Id = 10,
            WikiId = 101,
            DocumentId = 202,
            Content = "content",
            CreateTime = now,
            UpdateTime = now,
            CreateUserId = 1,
            UpdateUserId = 1,
            IsDeleted = 0,
        });

        await context.WikiDocumentChunkContents.AddAsync(new WikiDocumentChunkContentEntity
        {
            Id = 20,
            WikiId = 101,
            DocumentId = 202,
            SliceContent = "chunk content",
            SliceOrder = 1,
            SliceLength = 12,
            CreateTime = now,
            UpdateTime = now,
            CreateUserId = 1,
            UpdateUserId = 1,
            IsDeleted = 0,
        });

        if (includeMetadata)
        {
            await context.WikiDocumentChunkMetadata.AddAsync(new WikiDocumentChunkMetadatumEntity
            {
                Id = 1,
                WikiId = 101,
                DocumentId = 202,
                ChunkId = 20,
                MetadataType = 1,
                MetadataContent = "metadata",
                CreateTime = now,
                UpdateTime = now,
                CreateUserId = 1,
                UpdateUserId = 1,
                IsDeleted = 0,
            });
        }

        await context.SaveChangesAsync();
    }

    private sealed class TestDatabaseContext : DatabaseContext
    {
        public TestDatabaseContext(DbContextOptions options, IServiceProvider serviceProvider)
            : base(options, serviceProvider)
        {
        }

        protected override bool ShouldApplySeedData() => false;
    }

    private sealed class SqliteContextScope : IDisposable
    {
        private readonly bool _ownsConnection;

        public SqliteContextScope(TestDatabaseContext context, SqliteConnection connection, bool ownsConnection)
        {
            Context = context;
            Connection = connection;
            _ownsConnection = ownsConnection;
        }

        public TestDatabaseContext Context { get; }

        public SqliteConnection Connection { get; }

        public void Dispose()
        {
            Context.Dispose();
            if (_ownsConnection)
            {
                Connection.Dispose();
            }
        }
    }
}
