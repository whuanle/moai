using MoAI.KnowledgeGraph.Services;
using Xunit;

namespace MoAI.KnowledgeGraph.Tests;

public class KgCypherReadOnlyGuardTests
{
    [Theory]
    [InlineData("MATCH (n:KgNode {kgId: $kgId}) RETURN n.name LIMIT 10")]
    [InlineData("OPTIONAL MATCH (n {kgId: $kgId})-[r]->(m) RETURN count(*)")]
    [InlineData("MATCH (n {kgId: $kgId}) WHERE n.name = '上海' RETURN n")]
    [InlineData("// 查询港口\nMATCH (n {kgId: $kgId}) RETURN n.name")]
    [InlineData("MATCH (n {kgId: $kgId}) RETURN n.name;")]
    public void Validate_AllowsReadOnlyQueries(string cypher)
    {
        Assert.Null(KgCypherReadOnlyGuard.Validate(cypher));
    }

    [Theory]
    [InlineData("CREATE (n {kgId: $kgId})")]
    [InlineData("MATCH (n {kgId: $kgId}) DELETE n")]
    [InlineData("MATCH (n {kgId: $kgId}) SET n.name = 'x'")]
    [InlineData("CALL db.labels()")]
    [InlineData("MATCH (n {kgId: $kgId}) DETACH DELETE n")]
    [InlineData("MATCH (n {kgId: $kgId}) RETURN n; DROP DATABASE neo4j")]
    public void Validate_RejectsWriteStatements(string cypher)
    {
        Assert.NotNull(KgCypherReadOnlyGuard.Validate(cypher));
    }

    [Fact]
    public void Validate_WriteKeywordInsideStringLiteral_IsAllowed()
    {
        // 字符串字面量里的 CREATE 不触发黑名单（剥除后校验）
        Assert.Null(KgCypherReadOnlyGuard.Validate("MATCH (n {kgId: $kgId}) WHERE n.name = 'CREATE 关口' RETURN n"));
    }

    [Fact]
    public void Validate_EmptyAndOversized_AreRejected()
    {
        Assert.NotNull(KgCypherReadOnlyGuard.Validate(string.Empty));
        Assert.NotNull(KgCypherReadOnlyGuard.Validate(new string('a', 8001)));
    }
}
