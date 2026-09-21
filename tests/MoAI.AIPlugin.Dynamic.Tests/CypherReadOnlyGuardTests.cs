using MoAI.AIPlugin.Dynamic;
using Xunit;

namespace MoAI.AIPlugin.Dynamic.Tests;

/// <summary>
/// <see cref="CypherReadOnlyGuard"/> 测试.
/// </summary>
public class CypherReadOnlyGuardTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_EmptyCypher_ReturnsError(string? cypher)
    {
        var result = CypherReadOnlyGuard.Validate(cypher);

        Assert.False(string.IsNullOrEmpty(result));
    }

    [Fact]
    public void Validate_TooLongCypher_ReturnsError()
    {
        var cypher = "MATCH (n:KgNode {kgId: $kgId}) WHERE n.name CONTAINS '" + new string('a', 8001) + "' RETURN n";

        var result = CypherReadOnlyGuard.Validate(cypher);

        Assert.Contains("8000", result);
    }

    [Fact]
    public void Validate_SimpleReadOnlyQuery_ReturnsNull()
    {
        var result = CypherReadOnlyGuard.Validate("MATCH (n:KgNode {kgId: $kgId}) RETURN n.name LIMIT 20");

        Assert.Null(result);
    }

    [Fact]
    public void Validate_TrailingSemicolon_ReturnsNull()
    {
        var result = CypherReadOnlyGuard.Validate("MATCH (n:KgNode {kgId: $kgId}) RETURN n.name LIMIT 20;");

        Assert.Null(result);
    }

    [Theory]
    [InlineData("CREATE (n:KgNode {kgId: $kgId, name: 'x'}) RETURN n")]
    [InlineData("MATCH (n:KgNode {kgId: $kgId}) MERGE (m:KgNode {kgId: $kgId, name: 'x'}) RETURN m")]
    [InlineData("MATCH (n:KgNode {kgId: $kgId}) DETACH DELETE n")]
    [InlineData("MATCH (n:KgNode {kgId: $kgId}) DELETE n")]
    [InlineData("MATCH (n:KgNode {kgId: $kgId}) SET n.name = 'x' RETURN n")]
    [InlineData("MATCH (n:KgNode {kgId: $kgId}) REMOVE n.description RETURN n")]
    [InlineData("LOAD CSV FROM 'file:///a.csv' AS row RETURN row")]
    [InlineData("MATCH (n:KgNode {kgId: $kgId}) FOREACH (x IN [1] | SET n.y = x) RETURN n")]
    [InlineData("CALL db.labels() YIELD label RETURN label")]
    [InlineData("DROP INDEX ON :KgNode(id)")]
    public void Validate_ForbiddenKeyword_ReturnsErrorWithKeyword(string cypher)
    {
        var result = CypherReadOnlyGuard.Validate(cypher);

        Assert.False(string.IsNullOrEmpty(result));
    }

    [Fact]
    public void Validate_KeywordHiddenInComment_IsIgnored()
    {
        var result = CypherReadOnlyGuard.Validate("MATCH (n:KgNode {kgId: $kgId}) /* CREATE */ RETURN n");

        Assert.Null(result);
    }

    [Fact]
    public void Validate_KeywordInsideStringLiteral_ReturnsNull()
    {
        var result = CypherReadOnlyGuard.Validate("MATCH (n:KgNode {kgId: $kgId}) WHERE n.name = 'CREATE' RETURN n");

        Assert.Null(result);
    }

    [Fact]
    public void Validate_LowercaseForbidden_ReturnsError()
    {
        var result = CypherReadOnlyGuard.Validate("match (n:KgNode {kgId: $kgId}) create (m) return m");

        Assert.False(string.IsNullOrEmpty(result));
    }

    [Fact]
    public void Validate_MultipleStatements_ReturnsError()
    {
        var result = CypherReadOnlyGuard.Validate("MATCH (n:KgNode {kgId: $kgId}) RETURN n; MATCH (m) RETURN m");

        Assert.False(string.IsNullOrEmpty(result));
    }

    [Fact]
    public void Validate_NonReadOnlyLeadingKeyword_ReturnsError()
    {
        var result = CypherReadOnlyGuard.Validate("FOREACH (x IN [1] | SET n.y = x)");

        Assert.False(string.IsNullOrEmpty(result));
    }

    [Fact]
    public void Validate_PropertyNamedCreatedAt_IsNotFalsePositive()
    {
        var result = CypherReadOnlyGuard.Validate("MATCH (n:KgNode {kgId: $kgId}) WHERE n.createdAt > 1 RETURN n");

        Assert.Null(result);
    }
}
