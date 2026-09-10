using System.Threading;
using System.Threading.Tasks;
using MoAI.Database.Entities;
using MoAI.Database.Enums;
using MoAI.Infra.Defaults;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Services;
using MoAI.KnowledgeGraph.Models;
using MoAI.KnowledgeGraph.Services;
using MoAI.Team.Services;
using Moq;
using Xunit;

namespace MoAI.KnowledgeGraph.Tests;

public class KnowledgeGraphAuthorizerTests
{
    [Fact]
    public async Task RequireTeamRoleAsync_WhenNotMember_Throws404()
    {
        using var db = TestSqliteContext.Create();
        var teamService = new Mock<ITeamService>();
        teamService.Setup(x => x.GetMyRoleAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TeamRole?)null);
        var sut = CreateAuthorizer(db, teamService.Object);

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => sut.RequireTeamRoleAsync(7, adminOnly: false, CancellationToken.None));

        Assert.Equal(404, ex.StatusCode);
    }

    [Fact]
    public async Task RequireTeamRoleAsync_WhenMemberAndAdminOnly_Throws403()
    {
        using var db = TestSqliteContext.Create();
        var teamService = new Mock<ITeamService>();
        teamService.Setup(x => x.GetMyRoleAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TeamRole.Member);
        var sut = CreateAuthorizer(db, teamService.Object);

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => sut.RequireTeamRoleAsync(7, adminOnly: true, CancellationToken.None));

        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task RequireTeamRoleAsync_WhenAdmin_ReturnsRole()
    {
        using var db = TestSqliteContext.Create();
        var teamService = new Mock<ITeamService>();
        teamService.Setup(x => x.GetMyRoleAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TeamRole.Admin);
        var sut = CreateAuthorizer(db, teamService.Object);

        var role = await sut.RequireTeamRoleAsync(7, adminOnly: true, CancellationToken.None);

        Assert.Equal(TeamRole.Admin, role);
    }

    [Fact]
    public async Task AuthorizeManagedAsync_WhenConnected_Throws409()
    {
        using var db = TestSqliteContext.Create();
        db.Context.KnowledgeGraphs.Add(new KnowledgeGraphEntity { Id = 7, TeamId = 1, Name = "外部图", Description = string.Empty, Mode = KnowledgeGraphModes.Connected, Database = "ext" });
        await db.Context.SaveChangesAsync(CancellationToken.None);
        var teamService = new Mock<ITeamService>();
        teamService.Setup(x => x.GetMyRoleAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TeamRole.Admin);
        var sut = CreateAuthorizer(db, teamService.Object);

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => sut.AuthorizeManagedAsync(7, adminOnly: false, CancellationToken.None));

        Assert.Equal(409, ex.StatusCode);
    }

    [Fact]
    public async Task AuthorizeManagedAsync_WhenManaged_ReturnsGraph()
    {
        using var db = TestSqliteContext.Create();
        db.Context.KnowledgeGraphs.Add(new KnowledgeGraphEntity { Id = 7, TeamId = 1, Name = "托管图", Description = string.Empty, Mode = KnowledgeGraphModes.Managed });
        await db.Context.SaveChangesAsync(CancellationToken.None);
        var teamService = new Mock<ITeamService>();
        teamService.Setup(x => x.GetMyRoleAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TeamRole.Admin);
        var sut = CreateAuthorizer(db, teamService.Object);

        var (graph, role) = await sut.AuthorizeManagedAsync(7, adminOnly: false, CancellationToken.None);

        Assert.Equal(7, graph.Id);
        Assert.Equal(TeamRole.Admin, role);
    }

    private static KnowledgeGraphAuthorizer CreateAuthorizer(TestSqliteContext db, ITeamService teamService)
    {
        var userProvider = new Mock<IUserContextProvider>();
        userProvider.Setup(x => x.GetUserContext()).Returns(new DefaultUserContext { UserId = 99 });
        return new KnowledgeGraphAuthorizer(db.Context, teamService, userProvider.Object);
    }
}
