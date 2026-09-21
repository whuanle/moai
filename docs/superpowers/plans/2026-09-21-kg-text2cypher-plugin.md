# 知识图谱 Text2Cypher 查图动态插件 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 新增动态插件模板 `kg_cypher_query`：团队把托管图/接入图绑定为插件实例，Agent 对话模型直接写只读 Cypher 查询图谱（方案 A，对话原生）。

**Architecture:** 插件类在 `MoAI.AIPlugin.Dynamic`（只做参数校验与调度），实际执行走 `KnowledgeGraph.Core` 新服务 `KgCypherAccessService`（托管图强制 `$kgId` 隔离、接入图按库路由会话、schema 自描述摘要）；依赖方向 AIPlugin.Dynamic → KnowledgeGraph.Shared（接口）+ KnowledgeGraph.Core（DI 实现），无环。安全三层：`CypherReadOnlyGuard` 文本校验 + `$kgId` 参数注入 + 超时/行数截断。

**Tech Stack:** .NET 10 / Maomi 模块 / Neo4j.Driver 5.28（Memgraph 兼容）/ xUnit / React 19 + antd 5 / Node E2E。

**设计文档:** `docs/superpowers/specs/2026-09-21-kg-text2cypher-plugin-design.md`（本计划 §编号与设计文档对应）

**仓库硬约束提醒（来自 AGENTS.md）:** 禁手写 `AddScoped` 之外用 Maomi 特性注册服务类（`[InjectOnScoped]`）；`BusinessException` 必须显式设 `StatusCode`；审计/软删除由框架过滤（不要手写 `IsDeleted == 0`）；模型类面向 AI 用 `[Description]`（`System.ComponentModel`）而非 `[JsonPropertyName]`（与 `postgres_query` 先例一致）；`git add` 只加本任务文件（工作区有并行会话 WIP）。

---

## File Structure（总览）

| 文件 | 动作 | 职责 |
|---|---|---|
| `tests/MoAI.AIPlugin.Dynamic.Tests/MoAI.AIPlugin.Dynamic.Tests.csproj` | 新建 | 守卫单测工程（xUnit） |
| `tests/MoAI.AIPlugin.Dynamic.Tests/CypherReadOnlyGuardTests.cs` | 新建 | 只读守卫全量用例 |
| `src/aiplugin/MoAI.AIPlugin.Dynamic/CypherReadOnlyGuard.cs` | 新建 | Cypher 文本只读校验 |
| `src/aiplugin/MoAI.AIPlugin.Dynamic/MoAI.AIPlugin.Dynamic.csproj` | 修改 | 引用 KG.Shared + InternalsVisibleTo |
| `src/knowledgegraph/MoAI.KnowledgeGraph.Shared/Models/KgCypherAccessModels.cs` | 新建 | 查询结果/摘要 DTO |
| `src/knowledgegraph/MoAI.KnowledgeGraph.Shared/Services/IKgCypherAccessService.cs` | 新建 | 跨模块契约接口 |
| `src/knowledgegraph/MoAI.KnowledgeGraph.Core/Services/KgCypherAccessService.cs` | 新建 | 执行/摘要实现 |
| `src/knowledgegraph/MoAI.KnowledgeGraph.Core/KnowledgeGraphCoreModule.cs` | 修改 | 注册接口映射 |
| `src/aiplugin/MoAI.AIPlugin.Dynamic/Models/KgCypherQueryRequest.cs` | 新建 | 请求模型 |
| `src/aiplugin/MoAI.AIPlugin.Dynamic/Models/KgCypherQueryResponse.cs` | 新建 | 响应模型 |
| `src/aiplugin/MoAI.AIPlugin.Dynamic/Models/KgCypherQueryConfig.cs` | 新建 | 配置模型 |
| `src/aiplugin/MoAI.AIPlugin.Dynamic/Plugins/KgCypherQueryPlugin.cs` | 新建 | 插件模板类 |
| `src/teamplugin/MoAI.TeamPlugin.Core/Handlers/SaveTeamDynamicPluginCommandHandler.cs` | 修改 | kg_cypher_query 绑定校验 |
| `ui/src/pages/teams/plugins/TeamDynamicPluginPanel.tsx` | 修改 | 绑定图谱下拉 + 预填 |
| `ui/src/i18n/locales/zh-CN/common.json`、`en-US/common.json` | 修改 | i18n 键 |
| `local-dev/kg-text2cypher-e2e.mjs` | 新建 | KT-S1~S10 |
| `local-dev/dynamic-plugin-e2e.mjs` | 修改 | 模板注册断言 |
| 文档四件套 + AGENTS.md + rounds-log + 设计文档 | 修改 | 登记 |

任务顺序即依赖顺序：Task 1→2（守卫 TDD）→ 3（KG 契约）→ 4（KG 实现）→ 5（插件）→ 6（teamplugin 校验）→ 7（前端）→ 8（E2E）→ 9（文档收尾）。

---

### Task 1: 新建单测工程并写 CypherReadOnlyGuard 失败测试

**Files:**
- Create: `tests/MoAI.AIPlugin.Dynamic.Tests/MoAI.AIPlugin.Dynamic.Tests.csproj`
- Create: `tests/MoAI.AIPlugin.Dynamic.Tests/CypherReadOnlyGuardTests.cs`
- Modify: `src/aiplugin/MoAI.AIPlugin.Dynamic/MoAI.AIPlugin.Dynamic.csproj`（加 InternalsVisibleTo，guard 为 internal）

- [ ] **Step 1: 写测试 csproj**（照 `tests/MoAI.KnowledgeGraph.Tests/MoAI.KnowledgeGraph.Tests.csproj` 骨架，包版本集中在 Directory.Packages.props，勿写 Version）

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
    <NoWarn>$(NoWarn);CA1707;CS1591;SA1600</NoWarn>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\aiplugin\MoAI.AIPlugin.Dynamic\MoAI.AIPlugin.Dynamic.csproj" />
  </ItemGroup>

</Project>
```

- [ ] **Step 2: 在 `MoAI.AIPlugin.Dynamic.csproj` 追加 InternalsVisibleTo**（放最后一个 `</ItemGroup>` 之后、`</Project>` 之前；参照 KG Core csproj:31-33 的写法）

```xml
  <ItemGroup>
    <InternalsVisibleTo Include="MoAI.AIPlugin.Dynamic.Tests" />
  </ItemGroup>
```

- [ ] **Step 3: 写失败测试**

```csharp
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
    public void Validate_KeywordHiddenInComment_ReturnsError()
    {
        var result = CypherReadOnlyGuard.Validate("MATCH (n:KgNode {kgId: $kgId}) /* CREATE */ RETURN n");

        Assert.False(string.IsNullOrEmpty(result));
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
```

- [ ] **Step 4: 运行确认编译失败（CypherReadOnlyGuard 不存在）**

Run: `dotnet build tests/MoAI.AIPlugin.Dynamic.Tests/MoAI.AIPlugin.Dynamic.Tests.csproj`
Expected: FAIL，`CS0246: 未找到类型或命名空间名"CypherReadOnlyGuard"`

- [ ] **Step 5: Commit**

```bash
git add tests/MoAI.AIPlugin.Dynamic.Tests src/aiplugin/MoAI.AIPlugin.Dynamic/MoAI.AIPlugin.Dynamic.csproj
git commit -m "test(aiplugin): kg_cypher_query 只读守卫测试先行（红）"
```

---

### Task 2: 实现 CypherReadOnlyGuard 并转绿

**Files:**
- Create: `src/aiplugin/MoAI.AIPlugin.Dynamic/CypherReadOnlyGuard.cs`

- [ ] **Step 1: 实现**（校验算法结构对照 `SqlReadOnlyGuard`：剥注释/字符串 → 单语句 → 首关键字白名单 → 全文黑名单；Cypher 无美元引用串，参数 `$kgId` 不算关键字字符）

```csharp
using System;
using System.Collections.Generic;

namespace MoAI.AIPlugin.Dynamic;

/// <summary>
/// Cypher 只读守卫：剥除注释与字符串字面量后，校验单条语句、只读首关键字白名单与全文写关键字黑名单.
/// </summary>
/// <remarks>
/// 黑名单含 <c>CALL</c>：v1 不开放图算法/存储过程（设计文档 §7.1）。
/// 首关键字白名单（MATCH/OPTIONAL/UNWIND/WITH/RETURN）与黑名单互为纵深防御，与 <see cref="SqlReadOnlyGuard"/> 同构。
/// </remarks>
internal static class CypherReadOnlyGuard
{
    /// <summary>查询文本最大长度.</summary>
    private const int MaxQueryLength = 8000;

    /// <summary>允许作为语句首关键字的只读前缀.</summary>
    private static readonly HashSet<string> AllowedLeadingKeywords = new(StringComparer.Ordinal)
    {
        "MATCH",
        "OPTIONAL",
        "UNWIND",
        "WITH",
        "RETURN",
    };

    /// <summary>全文禁写的关键字（DML/DDL/过程调用）.</summary>
    private static readonly HashSet<string> ForbiddenKeywords = new(StringComparer.Ordinal)
    {
        "CREATE",
        "MERGE",
        "DELETE",
        "DETACH",
        "SET",
        "REMOVE",
        "LOAD",
        "FOREACH",
        "CALL",
        "DROP",
    };

    /// <summary>
    /// 校验 Cypher 查询文本.
    /// </summary>
    /// <param name="cypher">查询文本.</param>
    /// <returns>返回 null 表示通过，否则返回教学式错误信息.</returns>
    public static string? Validate(string? cypher)
    {
        if (string.IsNullOrWhiteSpace(cypher))
        {
            return "Cypher 不能为空";
        }

        if (cypher.Length > MaxQueryLength)
        {
            return $"Cypher 过长（超过 {MaxQueryLength} 字符），请缩小查询范围";
        }

        var text = StripCommentsAndLiterals(cypher);

        if (ContainsMultipleStatements(text))
        {
            return "只允许执行单条 Cypher 语句：不支持一次提交多条语句";
        }

        var leading = ReadLeadingKeyword(text);
        if (leading == null)
        {
            return "无法识别的 Cypher 语句";
        }

        if (!AllowedLeadingKeywords.Contains(leading))
        {
            return $"只允许执行只读 Cypher：不支持以 {leading} 开头的语句（仅允许 MATCH、OPTIONAL、UNWIND、WITH、RETURN）";
        }

        foreach (var keyword in ReadKeywords(text))
        {
            if (ForbiddenKeywords.Contains(keyword))
            {
                return $"只允许执行只读 Cypher：语句中不允许出现 {keyword}（本插件为只读查询，写操作请走图谱管理界面）";
            }
        }

        return null;
    }

    /// <summary>
    /// 剥除行注释、块注释与单双引号字符串字面量，替换为单个空格.
    /// </summary>
    private static string StripCommentsAndLiterals(string cypher)
    {
        var chars = new char[cypher.Length];
        var length = 0;
        var i = 0;
        while (i < cypher.Length)
        {
            var current = cypher[i];
            if (current == '/' && i + 1 < cypher.Length && cypher[i + 1] == '/')
            {
                chars[length++] = ' ';
                i = SkipToEndOfLine(cypher, i);
                continue;
            }

            if (current == '/' && i + 1 < cypher.Length && cypher[i + 1] == '*')
            {
                chars[length++] = ' ';
                i = SkipBlockComment(cypher, i);
                continue;
            }

            if (current == '\'' || current == '"')
            {
                chars[length++] = ' ';
                i = SkipQuoted(cypher, i, current);
                continue;
            }

            chars[length++] = current;
            i++;
        }

        return new string(chars, 0, length);
    }

    private static bool ContainsMultipleStatements(string text)
    {
        var seenSemicolon = false;
        foreach (var current in text)
        {
            if (current == ';')
            {
                seenSemicolon = true;
                continue;
            }

            if (seenSemicolon && !char.IsWhiteSpace(current))
            {
                return true;
            }
        }

        return false;
    }

    private static string? ReadLeadingKeyword(string text)
    {
        var index = 0;
        while (index < text.Length && char.IsWhiteSpace(text[index]))
        {
            index++;
        }

        var start = index;
        while (index < text.Length && IsKeywordChar(text[index]))
        {
            index++;
        }

        return index == start ? null : text[start..index].ToUpperInvariant();
    }

    private static IEnumerable<string> ReadKeywords(string text)
    {
        var index = 0;
        while (index < text.Length)
        {
            if (IsKeywordChar(text[index]))
            {
                var start = index;
                while (index < text.Length && IsKeywordChar(text[index]))
                {
                    index++;
                }

                yield return text[start..index].ToUpperInvariant();
            }
            else
            {
                index++;
            }
        }
    }

    /// <summary>
    /// 关键字字符：字母、数字、下划线。不含 <c>$</c>（参数前缀，<c>$kgId</c> 不参与关键字判定）.
    /// </summary>
    private static bool IsKeywordChar(char value) => char.IsLetterOrDigit(value) || value == '_';

    private static int SkipToEndOfLine(string cypher, int index)
    {
        while (index < cypher.Length && cypher[index] != '\n')
        {
            index++;
        }

        return index;
    }

    private static int SkipBlockComment(string cypher, int index)
    {
        index += 2;
        while (index + 1 < cypher.Length && !(cypher[index] == '*' && cypher[index + 1] == '/'))
        {
            index++;
        }

        return Math.Min(index + 2, cypher.Length);
    }

    private static int SkipQuoted(string cypher, int index, char quote)
    {
        index++;
        while (index < cypher.Length)
        {
            if (cypher[index] == '\\' && index + 1 < cypher.Length)
            {
                index += 2;
                continue;
            }

            if (cypher[index] == quote)
            {
                return index + 1;
            }

            index++;
        }

        return index;
    }
}
```

注意：`StripCommentsAndLiterals` 的 doc 注释刻意不写注释定界符字面序列，避免阅读歧义；实现照上方代码原样落盘即可。

- [ ] **Step 2: 运行测试转绿**

Run: `dotnet test tests/MoAI.AIPlugin.Dynamic.Tests/MoAI.AIPlugin.Dynamic.Tests.csproj`
Expected: PASS（Task 1 全部用例通过）

- [ ] **Step 3: 主工程编译不受影响**

Run: `dotnet build src/MoAI/MoAI.csproj`
Expected: 0 error

- [ ] **Step 4: Commit**

```bash
git add src/aiplugin/MoAI.AIPlugin.Dynamic/CypherReadOnlyGuard.cs
git commit -m "feat(aiplugin): CypherReadOnlyGuard 只读守卫（白名单前缀+黑名单关键字+剥注释字符串）"
```

---

### Task 3: KG Shared 契约（DTO + 接口）

**Files:**
- Create: `src/knowledgegraph/MoAI.KnowledgeGraph.Shared/Models/KgCypherAccessModels.cs`
- Create: `src/knowledgegraph/MoAI.KnowledgeGraph.Shared/Services/IKgCypherAccessService.cs`

- [ ] **Step 1: 写 DTO**（record 风格照 `KnowledgeGraphIntrospection.cs`）

```csharp
using System.Collections.Generic;

namespace MoAI.KnowledgeGraph.Models;

/// <summary>
/// 图 Cypher 只读查询结果（表格化）.
/// </summary>
/// <param name="Columns">结果列名（取自第一条记录的返回键）.</param>
/// <param name="Rows">结果行，每行为「列名 → 值」映射.</param>
/// <param name="RowCount">本次实际返回行数.</param>
/// <param name="Truncated">是否因行数上限被截断.</param>
public sealed record KgCypherQueryResult(
    IReadOnlyList<string> Columns,
    IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows,
    int RowCount,
    bool Truncated);

/// <summary>
/// 实体类型摘要项.
/// </summary>
/// <param name="Name">类型名.</param>
/// <param name="Description">描述.</param>
/// <param name="Properties">属性名清单.</param>
public sealed record KgCypherSchemaItem(string Name, string Description, IReadOnlyList<string> Properties);

/// <summary>
/// 关系类型摘要项.
/// </summary>
/// <param name="Name">类型名.</param>
/// <param name="FromTypeName">起始实体类型名（未约束为 null）.</param>
/// <param name="ToTypeName">目标实体类型名（未约束为 null）.</param>
/// <param name="Description">描述.</param>
public sealed record KgCypherRelationTypeItem(string Name, string? FromTypeName, string? ToTypeName, string Description);

/// <summary>
/// 节点名采样分组.
/// </summary>
/// <param name="TypeName">实体类型名（接入图为 label）.</param>
/// <param name="Names">采样节点名.</param>
public sealed record KgCypherSampleGroup(string TypeName, IReadOnlyList<string> Names);

/// <summary>
/// 图谱自描述摘要（供对话模型写 Cypher 前了解图结构）.
/// </summary>
/// <param name="GraphType">managed / connected.</param>
/// <param name="Dialect">图数据库方言（memgraph/neo4j）.</param>
/// <param name="EntityTypes">实体类型清单（接入图为空，用 PropertyKeys/采样代替）.</param>
/// <param name="RelationTypes">关系类型清单（接入图为空）.</param>
/// <param name="SampleNodes">各类型节点名采样.</param>
/// <param name="PropertyKeys">接入图属性键清单（托管图为空）.</param>
/// <param name="Usage">用法说明（含 $kgId 指引）.</param>
public sealed record KgCypherSchemaDigest(
    string GraphType,
    string Dialect,
    IReadOnlyList<KgCypherSchemaItem> EntityTypes,
    IReadOnlyList<KgCypherRelationTypeItem> RelationTypes,
    IReadOnlyList<KgCypherSampleGroup> SampleNodes,
    IReadOnlyList<string> PropertyKeys,
    string Usage);
```

- [ ] **Step 2: 写接口**

```csharp
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MoAI.KnowledgeGraph.Models;

namespace MoAI.KnowledgeGraph.Services;

/// <summary>
/// 知识图谱 Cypher 只读访问服务：供动态插件 kg_cypher_query 消费，托管图强制 $kgId 隔离，接入图按库路由只读会话.
/// </summary>
public interface IKgCypherAccessService
{
    /// <summary>
    /// 执行只读 Cypher 查询（文本守卫由调用方先行校验；托管图在此强制 $kgId）.
    /// </summary>
    /// <param name="knowledgeGraphId">图谱 id.</param>
    /// <param name="cypher">已过守卫的只读 Cypher.</param>
    /// <param name="parameters">查询参数（键不含 $ 前缀；托管图的 kgId 键被服务端覆盖注入）.</param>
    /// <param name="maxRows">返回行数上限.</param>
    /// <param name="timeoutSeconds">查询超时秒数.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>表格化结果.</returns>
    Task<KgCypherQueryResult> ExecuteQueryAsync(long knowledgeGraphId, string cypher, IReadOnlyDictionary<string, object?>? parameters, int maxRows, int timeoutSeconds, CancellationToken cancellationToken);

    /// <summary>
    /// 获取图谱自描述摘要.
    /// </summary>
    /// <param name="knowledgeGraphId">图谱 id.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>图结构摘要.</returns>
    Task<KgCypherSchemaDigest> GetSchemaDigestAsync(long knowledgeGraphId, CancellationToken cancellationToken);
}
```

- [ ] **Step 3: 编译**

Run: `dotnet build src/MoAI/MoAI.csproj`
Expected: 0 error

- [ ] **Step 4: Commit**

```bash
git add src/knowledgegraph/MoAI.KnowledgeGraph.Shared/Models/KgCypherAccessModels.cs src/knowledgegraph/MoAI.KnowledgeGraph.Shared/Services/IKgCypherAccessService.cs
git commit -m "feat(knowledgegraph): kg_cypher_query 跨模块契约（IKgCypherAccessService + DTO）"
```

---

### Task 4: KG Core 实现 KgCypherAccessService 并注册

**Files:**
- Create: `src/knowledgegraph/MoAI.KnowledgeGraph.Core/Services/KgCypherAccessService.cs`
- Modify: `src/knowledgegraph/MoAI.KnowledgeGraph.Core/KnowledgeGraphCoreModule.cs`

- [ ] **Step 1: 写实现**。要点（与设计文档 §6/§7.2/§7.3 一一对应）：

1. `ExecuteQueryAsync`：查图谱实体（404）→ 托管图校验查询含 `$kgId`（400 教学式）并强制注入 `kgId` 参数 → `GraphDriverProvider.GetRuntimeAsync` 取驱动 → 托管图默认会话、接入图按 `graph.Database` 路由（`OpenSession` 复制 `CypherKnowledgeGraphStore` 私有逻辑）→ `ExecuteReadAsync` 带 `TransactionConfigBuilder.WithTimeout` + 链接 `CancellationTokenSource.CancelAfter` 双保险 → 取 `maxRows+1` 行判截断 → 单元格归一（Node/Relationship/Path/列表/字典/长字符串截断 2000）。
2. `GetSchemaDigestAsync`：托管图查 `knowledge_graph_entity_type`/`knowledge_graph_relation_type`（`DatabaseContext.KnowledgeGraphEntityTypes`/`KnowledgeGraphRelationTypes`）+ 一条采样 Cypher（`MATCH (n:KgNode {kgId:$kgId}) RETURN n.entityTypeId AS entityTypeId, n.name AS name LIMIT 300`）内存分组每类型 3 个；接入图走 `IKnowledgeGraphIntrospectionCache.GetAsync` + 一条采样 Cypher（`MATCH (n) RETURN labels(n) AS labels, coalesce(toString(n.name), toString(n.title), toString(n.id), '') AS name LIMIT 100`）按首标签分组，标签数截 10。
3. `[InjectOnScoped]` 特性 + 模块注册接口映射（照 `KnowledgeGraphAuthorizer` 模式）。

完整代码：

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Maomi;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.KnowledgeGraph.Models;
using MoAI.Settings.Models;
using Neo4j.Driver;

namespace MoAI.KnowledgeGraph.Services;

/// <summary>
/// 知识图谱 Cypher 只读访问服务：供动态插件 kg_cypher_query 调用.
/// </summary>
/// <remarks>
/// 托管图多图共用一个 Bolt 库、靠 kgId 属性逻辑隔离，因此强制查询携带 $kgId 参数并由服务端注入真实图谱 id；
/// 接入图整库即图、天然隔离，按 neo4j 方言路由 database 会话（与 CypherKnowledgeGraphStore.OpenSession 同规则）。
/// </remarks>
[InjectOnScoped]
public class KgCypherAccessService : IKgCypherAccessService
{
    /// <summary>单元格字符串最大长度.</summary>
    private const int MaxCellStringLength = 2000;

    /// <summary>列表/字典单元格的最大元素数.</summary>
    private const int MaxCellItems = 50;

    /// <summary>嵌套归一的最大深度.</summary>
    private const int MaxCellDepth = 3;

    /// <summary>托管图 schema 采样的最大节点数.</summary>
    private const int MaxManagedSampleNodes = 300;

    /// <summary>每类型采样节点名个数.</summary>
    private const int SampleNamesPerType = 3;

    /// <summary>接入图摘要处理的最大标签数.</summary>
    private const int MaxConnectedLabels = 10;

    private readonly GraphDriverProvider _provider;
    private readonly DatabaseContext _databaseContext;
    private readonly IKnowledgeGraphIntrospectionCache _introspectionCache;

    /// <summary>
    /// Initializes a new instance of the <see cref="KgCypherAccessService"/> class.
    /// </summary>
    public KgCypherAccessService(GraphDriverProvider provider, DatabaseContext databaseContext, IKnowledgeGraphIntrospectionCache introspectionCache)
    {
        _provider = provider;
        _databaseContext = databaseContext;
        _introspectionCache = introspectionCache;
    }

    /// <inheritdoc/>
    public async Task<KgCypherQueryResult> ExecuteQueryAsync(long knowledgeGraphId, string cypher, IReadOnlyDictionary<string, object?>? parameters, int maxRows, int timeoutSeconds, CancellationToken cancellationToken)
    {
        var graph = await GetGraphAsync(knowledgeGraphId, cancellationToken);
        var managed = !string.Equals(graph.Mode, KnowledgeGraphModes.Connected, StringComparison.Ordinal);
        if (managed && !cypher.Contains("$kgId", StringComparison.Ordinal))
        {
            throw new BusinessException("托管图谱查询必须包含 {kgId: $kgId} 过滤以隔离图谱数据，例如：MATCH (n:KgNode {kgId: $kgId}) RETURN n.name LIMIT 20；$kgId 参数由系统自动注入，请在查询中使用后重试") { StatusCode = 400 };
        }

        var (driver, dialect) = await _provider.GetRuntimeAsync(cancellationToken);

        var queryParameters = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (parameters != null)
        {
            foreach (var pair in parameters)
            {
                queryParameters[pair.Key] = pair.Value;
            }
        }

        if (managed)
        {
            queryParameters["kgId"] = knowledgeGraphId;
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(timeoutSeconds, 1, 300)));

        await using var session = OpenSession(driver, managed ? null : graph.Database, dialect);
        var (records, truncated) = await FetchAsync(session, cypher, queryParameters, maxRows, Math.Clamp(timeoutSeconds, 1, 300), timeoutCts.Token);

        var columns = records.Count > 0 ? records[0].Keys.ToList() : new List<string>();
        var rows = new List<IReadOnlyDictionary<string, object?>>(records.Count);
        foreach (var record in records)
        {
            var row = new Dictionary<string, object?>(columns.Count, StringComparer.Ordinal);
            foreach (var key in columns)
            {
                row[key] = NormalizeCell(record[key]);
            }

            rows.Add(row);
        }

        return new KgCypherQueryResult(columns, rows, rows.Count, truncated);
    }

    /// <inheritdoc/>
    public async Task<KgCypherSchemaDigest> GetSchemaDigestAsync(long knowledgeGraphId, CancellationToken cancellationToken)
    {
        var graph = await GetGraphAsync(knowledgeGraphId, cancellationToken);
        var (driver, dialect) = await _provider.GetRuntimeAsync(cancellationToken);

        if (string.Equals(graph.Mode, KnowledgeGraphModes.Connected, StringComparison.Ordinal))
        {
            return await BuildConnectedDigestAsync(graph, driver, dialect, cancellationToken);
        }

        return await BuildManagedDigestAsync(graph, driver, dialect, cancellationToken);
    }

    private async Task<KgCypherSchemaDigest> BuildManagedDigestAsync(Database.Entities.KnowledgeGraphEntity graph, IDriver driver, string dialect, CancellationToken cancellationToken)
    {
        var entityTypes = await _databaseContext.KnowledgeGraphEntityTypes.AsNoTracking()
            .Where(x => x.KnowledgeGraphId == graph.Id)
            .OrderBy(x => x.Sort).ThenBy(x => x.Id)
            .Select(x => new { x.Id, x.Name, x.Description, x.Properties })
            .ToListAsync(cancellationToken);
        var relationTypes = await _databaseContext.KnowledgeGraphRelationTypes.AsNoTracking()
            .Where(x => x.KnowledgeGraphId == graph.Id)
            .OrderBy(x => x.Sort).ThenBy(x => x.Id)
            .Select(x => new { x.Id, x.Name, x.Description, x.SourceTypeId, x.TargetTypeId })
            .ToListAsync(cancellationToken);

        var nameById = entityTypes.ToDictionary(x => x.Id, x => x.Name);
        var sampleCypher = "MATCH (n:KgNode {kgId: $kgId}) RETURN n.entityTypeId AS entityTypeId, n.name AS name LIMIT $limit";
        var sampleRecords = await ReadInternalAsync(driver, null, dialect, sampleCypher, new { kgId = graph.Id, limit = MaxManagedSampleNodes }, cancellationToken);
        var sampleGroups = sampleRecords
            .Select(r => (TypeId: r["entityTypeId"].As<long>(), Name: r["name"].As<string>()))
            .GroupBy(x => x.TypeId)
            .Select(g => new KgCypherSampleGroup(
                nameById.GetValueOrDefault(g.Key) ?? g.Key.ToString(),
                g.Select(x => x.Name).Take(SampleNamesPerType).ToList()))
            .ToList();

        var usage = "托管图谱：节点标签固定为 KgNode（属性 id/kgId/entityTypeId/name/description/propsJson），边类型固定为 KG_REL（属性 relationTypeId）。" +
            "所有 MATCH 必须带 {kgId: $kgId} 过滤，$kgId 由系统自动注入、请勿自行赋值；写操作不支持。";

        return new KgCypherSchemaDigest(
            "managed",
            dialect,
            entityTypes.Select(x => new KgCypherSchemaItem(x.Name, x.Description, ParsePropertyNames(x.Properties))).ToList(),
            relationTypes.Select(x => new KgCypherRelationTypeItem(
                x.Name,
                x.SourceTypeId is long s ? nameById.GetValueOrDefault(s) : null,
                x.TargetTypeId is long t ? nameById.GetValueOrDefault(t) : null,
                x.Description)).ToList(),
            sampleGroups,
            Array.Empty<string>(),
            usage);
    }

    private async Task<KgCypherSchemaDigest> BuildConnectedDigestAsync(Database.Entities.KnowledgeGraphEntity graph, IDriver driver, string dialect, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(graph.Database))
        {
            throw new BusinessException("接入图谱缺少数据库配置.") { StatusCode = 409 };
        }

        var (introspection, _, _) = await _introspectionCache.GetAsync(graph.Id, graph.Database, refresh: false, cancellationToken);
        var sampleCypher = "MATCH (n) RETURN labels(n) AS labels, coalesce(toString(n.name), toString(n.title), toString(n.id), '') AS name LIMIT 100";
        var sampleRecords = await ReadInternalAsync(driver, graph.Database, dialect, sampleCypher, new { }, cancellationToken);
        var sampleGroups = sampleRecords
            .Select(r => (Labels: r["labels"].As<List<object>>().Select(x => x.As<string>()).ToList(), Name: r["name"].As<string>()))
            .Where(x => x.Labels.Count > 0)
            .GroupBy(x => x.Labels[0])
            .Take(MaxConnectedLabels)
            .Select(g => new KgCypherSampleGroup(g.Key, g.Select(x => x.Name).Where(n => !string.IsNullOrEmpty(n)).Take(SampleNamesPerType).ToList()))
            .ToList();

        var usage = $"接入图谱（外部 {dialect}）：节点使用原生 label、边使用原生关系类型，无 kgId 属性、查询无需 $kgId 过滤。只读查询，写操作不支持。";

        return new KgCypherSchemaDigest(
            "connected",
            dialect,
            Array.Empty<KgCypherSchemaItem>(),
            Array.Empty<KgCypherRelationTypeItem>(),
            sampleGroups,
            introspection.PropertyKeys.ToList(),
            usage);
    }

    private async Task<Database.Entities.KnowledgeGraphEntity> GetGraphAsync(long knowledgeGraphId, CancellationToken cancellationToken)
    {
        var graph = await _databaseContext.KnowledgeGraphs.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == knowledgeGraphId, cancellationToken);
        if (graph == null)
        {
            throw new BusinessException("图谱不存在") { StatusCode = 404 };
        }

        return graph;
    }

    /// <summary>
    /// 内部采样查询（不走 $kgId 校验，接入图按库路由）.
    /// </summary>
    private static async Task<List<IRecord>> ReadInternalAsync(IDriver driver, string? database, string dialect, string cypher, object parameters, CancellationToken cancellationToken)
    {
        await using var session = OpenSession(driver, database, dialect);
        return await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(cypher, parameters);
            return await cursor.ToListAsync(cancellationToken);
        }, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// 与 CypherKnowledgeGraphStore.OpenSession 同规则：仅 neo4j 方言按库名路由，memgraph 恒用默认库.
    /// </summary>
    private static IAsyncSession OpenSession(IDriver driver, string? database, string dialect)
    {
        var useDatabase = string.Equals(dialect, KnowledgeGraphStoreSettings.DialectNeo4j, StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(database);
        return useDatabase
            ? driver.AsyncSession(b => b.WithDatabase(database!))
            : driver.AsyncSession();
    }

    private static async Task<(List<IRecord> Records, bool Truncated)> FetchAsync(IAsyncSession session, string cypher, Dictionary<string, object?> parameters, int maxRows, int timeoutSeconds, CancellationToken cancellationToken)
    {
        var records = await session.ExecuteReadAsync(
            async tx =>
            {
                var cursor = await tx.RunAsync(cypher, parameters);
                var list = new List<IRecord>(maxRows + 1);
                while (await cursor.FetchAsync())
                {
                    list.Add(cursor.Current);
                    if (list.Count > maxRows)
                    {
                        break;
                    }
                }

                return list;
            },
            action => action.WithTimeout(TimeSpan.FromSeconds(timeoutSeconds)),
            cancellationToken);
        var truncated = records.Count > maxRows;
        if (truncated)
        {
            records = records.Take(maxRows).ToList();
        }

        return (records, truncated);
    }

    private static object? NormalizeCell(object? value, int depth = 0)
    {
        switch (value)
        {
            case null:
                return null;
            case string text:
                return TruncateString(text);
            case bool or long or int or double or float:
                return value;
            case INode node:
            {
                if (depth >= MaxCellDepth)
                {
                    return TruncateString(node.ToString());
                }

                var nodeDict = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["_id"] = node.Id,
                    ["_labels"] = node.Labels.ToList(),
                };
                foreach (var property in node.Properties)
                {
                    nodeDict[property.Key] = NormalizeCell(property.Value, depth + 1);
                }

                return nodeDict;
            }

            case IRelationship relationship:
            {
                if (depth >= MaxCellDepth)
                {
                    return TruncateString(relationship.ToString());
                }

                var relDict = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["_id"] = relationship.Id,
                    ["_type"] = relationship.Type,
                };
                foreach (var property in relationship.Properties)
                {
                    relDict[property.Key] = NormalizeCell(property.Value, depth + 1);
                }

                return relDict;
            }

            case IPath path:
                return new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["_pathNodeCount"] = (long)path.Nodes.Count,
                    ["_pathRelationshipCount"] = (long)path.Relationships.Count,
                };

            case DateTimeOffset dateTimeOffset:
                return dateTimeOffset.ToString("O");
            case DateTime dateTime:
                return dateTime.ToString("O");
            case IDictionary<string, object?> dictionary when depth < MaxCellDepth:
                return dictionary.Take(MaxCellItems).ToDictionary(p => p.Key, p => NormalizeCell(p.Value, depth + 1), StringComparer.Ordinal);
            case IEnumerable<object?> enumerable when depth < MaxCellDepth:
                return enumerable.Take(MaxCellItems).Select(x => NormalizeCell(x, depth + 1)).ToList();
            default:
                return TruncateString(value.ToString() ?? string.Empty);
        }
    }

    private static string TruncateString(string text)
    {
        return text.Length <= MaxCellStringLength ? text : text[..MaxCellStringLength] + "…(已截断)";
    }

    private static IReadOnlyList<string> ParsePropertyNames(string? propertiesJson)
    {
        if (string.IsNullOrWhiteSpace(propertiesJson))
        {
            return Array.Empty<string>();
        }

        try
        {
            using var document = JsonDocument.Parse(propertiesJson);
            var names = new List<string>();
            foreach (var element in document.RootElement.EnumerateArray())
            {
                if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String)
                {
                    names.Add(name.GetString()!);
                }
            }

            return names;
        }
        catch (JsonException)
        {
            return Array.Empty<string>();
        }
    }
}
```

注意两点：① `IDictionary<string, object?>` 分支必须在 `IEnumerable<object?>` 之前（字典也实现 IEnumerable）；② `cursor.FetchAsync()` 循环是 Neo4j.Driver 5.x 惯用法，`ToListAsync(ct)` 的取消令牌重载在 5.28 存在，若编译报错则去掉该参数（链接 CTS 已兜底超时）。

- [ ] **Step 2: 注册接口映射**。在 `KnowledgeGraphCoreModule.ConfigureServices` 末尾追加（照 `IKnowledgeGraphAuthorizer` 行的模式）：

```csharp
        context.Services.AddScoped<IKgCypherAccessService>(sp => sp.GetRequiredService<KgCypherAccessService>());
```

- [ ] **Step 3: 编译**

Run: `dotnet build src/MoAI/MoAI.csproj`
Expected: 0 error。若 `ExecuteReadAsync` 的 `action => action.WithTimeout(...)` 重载签名不匹配（5.28 应存在 `Func<IAsyncTransaction,Task<T>>, Action<TransactionConfigBuilder>, CancellationToken`），按编译器提示调整重载参数顺序；链接 CTS 已保证超时语义，必要时可去掉 WithTimeout 行。

- [ ] **Step 4: Commit**

```bash
git add src/knowledgegraph/MoAI.KnowledgeGraph.Core/Services/KgCypherAccessService.cs src/knowledgegraph/MoAI.KnowledgeGraph.Core/KnowledgeGraphCoreModule.cs
git commit -m "feat(knowledgegraph): KgCypherAccessService——托管图 $kgId 强制隔离 + 接入图按库路由 + schema 自描述摘要"
```

---

### Task 5: AIPlugin.Dynamic 插件模板（模型 + 插件类）

**Files:**
- Modify: `src/aiplugin/MoAI.AIPlugin.Dynamic/MoAI.AIPlugin.Dynamic.csproj`（引用 KG.Shared）
- Create: `src/aiplugin/MoAI.AIPlugin.Dynamic/Models/KgCypherQueryRequest.cs`
- Create: `src/aiplugin/MoAI.AIPlugin.Dynamic/Models/KgCypherQueryConfig.cs`
- Create: `src/aiplugin/MoAI.AIPlugin.Dynamic/Models/KgCypherQueryResponse.cs`
- Create: `src/aiplugin/MoAI.AIPlugin.Dynamic/Plugins/KgCypherQueryPlugin.cs`

- [ ] **Step 1: csproj 追加项目引用**（放在现有 `MoAI.AIPlugin.Shared` 引用的同一个 `ItemGroup` 内）

```xml
		<ProjectReference Include="..\..\knowledgegraph\MoAI.KnowledgeGraph.Shared\MoAI.KnowledgeGraph.Shared.csproj" />
```

- [ ] **Step 2: 三个模型**（照 `PostgresQuery*` 惯例：`[Description]`、无 `[JsonPropertyName]`）

`KgCypherQueryRequest.cs`：

```csharp
using System.Collections.Generic;
using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// 知识图谱只读查询插件请求参数.
/// </summary>
public class KgCypherQueryRequest
{
    /// <summary>
    /// 要执行的只读 Cypher 查询.
    /// </summary>
    [Description("要执行的只读 Cypher 查询语句。托管图谱必须包含 {kgId: $kgId} 过滤（$kgId 由系统自动注入，无需赋值），例如 MATCH (n:KgNode {kgId: $kgId}) WHERE n.name CONTAINS $kw RETURN n.name, n.description LIMIT 20")]
    public string Cypher { get; set; } = string.Empty;

    /// <summary>
    /// Cypher 查询参数.
    /// </summary>
    [Description("Cypher 查询参数对象：键为 $ 参数名（不含 $ 前缀），值只能是字符串/数字/布尔；kgId 为系统保留键，传入会被覆盖")]
    public Dictionary<string, object?>? Params { get; set; }

    /// <summary>
    /// 是否返回图谱自描述.
    /// </summary>
    [Description("置为 true 时返回图谱自描述摘要（实体类型、关系类型、示例节点与用法说明），此时忽略 Cypher；首次查询前建议先取摘要")]
    public bool Schema { get; set; }
}
```

`KgCypherQueryConfig.cs`：

```csharp
using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// 知识图谱只读查询插件配置（每个实例独立保存）.
/// </summary>
public class KgCypherQueryConfig
{
    /// <summary>
    /// 绑定的知识图谱 id.
    /// </summary>
    [Description("要绑定的知识图谱 id（本团队的托管图或接入图）")]
    public long KgId { get; set; }

    /// <summary>
    /// 单次最多返回行数.
    /// </summary>
    [Description("单次最多返回行数，取值 1-1000（默认 200）；超出部分被丢弃，并把响应中的 Truncated 置为 true")]
    public int MaxRows { get; set; } = 200;

    /// <summary>
    /// 查询超时秒数.
    /// </summary>
    [Description("查询超时秒数，取值 1-300（默认 30）；超时后查询被取消并返回可读失败")]
    public int TimeoutSeconds { get; set; } = 30;
}
```

`KgCypherQueryResponse.cs`：

```csharp
using System.Collections.Generic;
using System.ComponentModel;
using MoAI.KnowledgeGraph.Models;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// 知识图谱只读查询插件响应结果：Schema=true 时填摘要字段，否则填表格字段.
/// </summary>
public class KgCypherQueryResponse
{
    /// <summary>结果列名.</summary>
    [Description("查询模式：结果列名，按查询返回顺序排列")]
    public IReadOnlyList<string> Columns { get; set; } = [];

    /// <summary>结果行.</summary>
    [Description("查询模式：结果行，每行为列名到值的映射；节点返回 {_id, _labels, 属性...}，关系返回 {_id, _type, 属性...}，超长字符串截断为 2000 字符")]
    public IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows { get; set; } = [];

    /// <summary>本次返回行数.</summary>
    [Description("查询模式：本次实际返回行数")]
    public int RowCount { get; set; }

    /// <summary>是否被截断.</summary>
    [Description("查询模式：是否因超过 maxRows 被截断；true 表示图中还有更多结果未返回")]
    public bool Truncated { get; set; }

    /// <summary>图谱类型.</summary>
    [Description("自描述模式：图谱类型 managed（托管）/ connected（外部接入）")]
    public string? GraphType { get; set; }

    /// <summary>图数据库方言.</summary>
    [Description("自描述模式：图数据库方言 memgraph/neo4j")]
    public string? Dialect { get; set; }

    /// <summary>实体类型清单.</summary>
    [Description("自描述模式：实体类型清单（名称、描述、属性）")]
    public IReadOnlyList<KgCypherSchemaItem> EntityTypes { get; set; } = [];

    /// <summary>关系类型清单.</summary>
    [Description("自描述模式：关系类型清单（名称、起止类型、描述）")]
    public IReadOnlyList<KgCypherRelationTypeItem> RelationTypes { get; set; } = [];

    /// <summary>采样节点.</summary>
    [Description("自描述模式：各类型节点名采样")]
    public IReadOnlyList<KgCypherSampleGroup> SampleNodes { get; set; } = [];

    /// <summary>用法说明.</summary>
    [Description("自描述模式：该图谱的查询用法说明（含 $kgId 指引）")]
    public string? Usage { get; set; }
}
```

- [ ] **Step 3: 插件类**（骨架照 `PostgresQueryPlugin`：常量钳制 + InitAsync 缓存配置 + RunAsync 校验先于执行；schema 模式不走守卫）

```csharp
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MoAI.AIPlugin.Attributes;
using MoAI.AIPlugin.Contracts;
using MoAI.AIPlugin.Dynamic.Models;
using MoAI.Infra.Exceptions;
using MoAI.KnowledgeGraph.Services;

namespace MoAI.AIPlugin.Dynamic.Plugins;

/// <summary>
/// 知识图谱只读查询（动态插件 Text2Cypher）：绑定一张团队图谱，对话模型直接写只读 Cypher 查询实体与关系.
/// </summary>
/// <remarks>
/// 只读与隔离分三层（见 <see cref="CypherReadOnlyGuard"/> 与 <c>IKgCypherAccessService</c> 实现）：
/// <list type="number">
/// <item><description>**文本层**：黑名单关键字/多语句/超长在 <see cref="CypherReadOnlyGuard"/> 拒绝.</description></item>
/// <item><description>**隔离层**：托管图强制 $kgId 参数并由服务端注入真实图谱 id；接入图按库路由天然隔离.</description></item>
/// <item><description>**资源层**：事务超时 + 行数截断在访问服务内兜底.</description></item>
/// </list>
/// Cypher 由对话模型生成（设计文档方案 A），纠错回路靠教学式错误信息回喂对话循环，无内置重试.
/// </remarks>
[AiPlugin(
    key: "kg_cypher_query",
    Name = "知识图谱只读查询",
    Description = "绑定一张知识图谱，用只读 Cypher 查询其中的实体与关系。先传 {\"Schema\": true} 获取图谱结构与示例节点，再写 MATCH 查询；托管图谱查询必须包含 {kgId: $kgId} 过滤（参数自动注入）")]
public class KgCypherQueryPlugin : IDynamicPluginRuntime<KgCypherQueryRequest, KgCypherQueryResponse, KgCypherQueryConfig>
{
    /// <summary>返回行数上限的下界.</summary>
    private const int MinMaxRows = 1;

    /// <summary>返回行数上限的上界.</summary>
    private const int MaxMaxRows = 1000;

    /// <summary>查询超时的下界（秒）.</summary>
    private const int MinTimeoutSeconds = 1;

    /// <summary>查询超时的上界（秒）.</summary>
    private const int MaxTimeoutSeconds = 300;

    private readonly IKgCypherAccessService _accessService;
    private KgCypherQueryConfig _config = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="KgCypherQueryPlugin"/> class.
    /// </summary>
    public KgCypherQueryPlugin(IKgCypherAccessService accessService)
    {
        _accessService = accessService;
    }

    /// <inheritdoc/>
    public static string GetParamsExampleValue()
    {
        return """
            {
              "Schema": true
              // 查询模式示例：
              // "Cypher": "MATCH (n:KgNode {kgId: $kgId}) WHERE n.name CONTAINS $kw RETURN n.name LIMIT 20",
              // "Params": { "kw": "仓库" }
            }
            """;
    }

    /// <inheritdoc/>
    public static string GetConfigExampleValue()
    {
        return """
            {
              "KgId": 123,          // 绑定的知识图谱 id
              "MaxRows": 200,       // 单次最多返回行数，1-1000
              "TimeoutSeconds": 30  // 查询超时秒数，1-300
            }
            """;
    }

    /// <inheritdoc/>
    public Task<string?> InitAsync(KgCypherQueryConfig config)
    {
        if (config.KgId <= 0)
        {
            return Task.FromResult<string?>("配置 KgId 必须大于 0（绑定要查询的知识图谱 id）");
        }

        _config = new KgCypherQueryConfig
        {
            KgId = config.KgId,
            MaxRows = Math.Clamp(config.MaxRows, MinMaxRows, MaxMaxRows),
            TimeoutSeconds = Math.Clamp(config.TimeoutSeconds, MinTimeoutSeconds, MaxTimeoutSeconds),
        };

        return Task.FromResult<string?>(null);
    }

    /// <inheritdoc/>
    public async Task<KgCypherQueryResponse> RunAsync(KgCypherQueryRequest request, CancellationToken cancellationToken)
    {
        if (request.Schema)
        {
            var digest = await _accessService.GetSchemaDigestAsync(_config.KgId, cancellationToken).ConfigureAwait(false);
            return new KgCypherQueryResponse
            {
                GraphType = digest.GraphType,
                Dialect = digest.Dialect,
                EntityTypes = digest.EntityTypes,
                RelationTypes = digest.RelationTypes,
                SampleNodes = digest.SampleNodes,
                Usage = digest.Usage,
            };
        }

        var violation = CypherReadOnlyGuard.Validate(request.Cypher);
        if (violation != null)
        {
            throw new BusinessException(400, violation);
        }

        var result = await _accessService.ExecuteQueryAsync(
            _config.KgId,
            request.Cypher,
            NormalizeParams(request.Params),
            _config.MaxRows,
            _config.TimeoutSeconds,
            cancellationToken).ConfigureAwait(false);

        return new KgCypherQueryResponse
        {
            Columns = result.Columns,
            Rows = result.Rows,
            RowCount = result.RowCount,
            Truncated = result.Truncated,
        };
    }

    /// <summary>
    /// 把 Params 里的 JsonElement 归一为基础 CLR 类型，供图库驱动使用.
    /// </summary>
    private static Dictionary<string, object?>? NormalizeParams(Dictionary<string, object?>? parameters)
    {
        if (parameters == null || parameters.Count == 0)
        {
            return null;
        }

        var result = new Dictionary<string, object?>(parameters.Count, StringComparer.Ordinal);
        foreach (var pair in parameters)
        {
            result[pair.Key] = pair.Value switch
            {
                null => null,
                JsonElement element => ConvertElement(element),
                _ => pair.Value,
            };
        }

        return result;
    }

    private static object? ConvertElement(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number => element.TryGetInt64(out var longValue) ? longValue : element.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => null,
        _ => element.GetRawText(),
    };
}
```

- [ ] **Step 4: 编译 + 全量单测**

Run: `dotnet build src/MoAI/MoAI.csproj && dotnet test tests/MoAI.AIPlugin.Dynamic.Tests/MoAI.AIPlugin.Dynamic.Tests.csproj`
Expected: build 0 error；测试 PASS（插件经 `PluginRegistry` 自动扫描注册，无需手工注册）

- [ ] **Step 5: Commit**

```bash
git add src/aiplugin/MoAI.AIPlugin.Dynamic
git commit -m "feat(aiplugin): kg_cypher_query 动态插件模板——schema 自描述 + 只读 Cypher 查询图谱"
```

---

### Task 6: teamplugin 保存实例时校验图谱归属

**Files:**
- Modify: `src/teamplugin/MoAI.TeamPlugin.Core/Handlers/SaveTeamDynamicPluginCommandHandler.cs`

**背景事实（已核实）**：保存时当前没有任何模板级配置校验（`Config` 原样入库，运行时才由 `PluginExecutor` 首次校验）；模板存在性检查在 Handler :45-49；`DatabaseContext.KnowledgeGraphs` 为图谱实体集。此校验无单测工程承载（teamplugin 无测试工程），由 E2E KT-S2 覆盖。

- [ ] **Step 1: Handler 构造注入 `DatabaseContext`**（若构造函数尚无；加 `using System.Text.Json; using Microsoft.EntityFrameworkCore; using MoAI.Database;`），并新增常量与校验方法：

```csharp
    /// <summary>
    /// kg_cypher_query 模板 key：实例配置必须绑定本团队的知识图谱.
    /// </summary>
    private const string KgCypherTemplateKey = "kg_cypher_query";

    /// <summary>
    /// kg_cypher_query 实例：校验配置里的 kgId 存在且属于本团队（创建与更新都校验）.
    /// </summary>
    private async Task EnsureKgBindingValidAsync(SaveTeamDynamicPluginCommand request, CancellationToken cancellationToken)
    {
        if (!string.Equals(request.TempleteKey, KgCypherTemplateKey, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        long kgId;
        try
        {
            var config = JsonDocument.Parse(request.Config).RootElement;
            if (!config.TryGetProperty("kgId", out var kgElement) && !config.TryGetProperty("KgId", out kgElement))
            {
                throw new BusinessException("kg_cypher_query 配置必须包含 kgId（绑定的知识图谱 id）.") { StatusCode = 400 };
            }

            if (!kgElement.TryGetInt64(out kgId) || kgId <= 0)
            {
                throw new BusinessException("kg_cypher_query 配置的 kgId 必须大于 0.") { StatusCode = 400 };
            }
        }
        catch (JsonException)
        {
            throw new BusinessException("kg_cypher_query 配置必须是合法 JSON.") { StatusCode = 400 };
        }

        var graph = await _databaseContext.KnowledgeGraphs.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == kgId, cancellationToken);
        if (graph == null)
        {
            throw new BusinessException("绑定的知识图谱不存在.") { StatusCode = 404 };
        }

        if (graph.TeamId != request.TeamId)
        {
            throw new BusinessException("只能绑定本团队的知识图谱.") { StatusCode = 403 };
        }
    }
```

- [ ] **Step 2: 在 `Handle` 中调用**——紧跟模板存在性检查（`模板不存在 404` 那段）之后、创建/更新分支之前：

```csharp
        await EnsureKgBindingValidAsync(request, cancellationToken);
```

- [ ] **Step 3: 编译**

Run: `dotnet build src/MoAI/MoAI.csproj`
Expected: 0 error

- [ ] **Step 4: Commit**

```bash
git add src/teamplugin/MoAI.TeamPlugin.Core/Handlers/SaveTeamDynamicPluginCommandHandler.cs
git commit -m "feat(teamplugin): kg_cypher_query 实例保存校验图谱存在与团队归属（403/404）"
```

---

### Task 7: 前端——团队动态插件弹窗绑定图谱并预填

**Files:**
- Modify: `ui/src/pages/teams/plugins/TeamDynamicPluginPanel.tsx`
- Modify: `ui/src/i18n/locales/zh-CN/common.json`、`ui/src/i18n/locales/en-US/common.json`

**背景事实（已核实）**：模板 Select 的 `onChange` 已把 `tp.configExample` 填进 config 编辑器（:292-309 一带）；`getKnowledgeGraphs(teamId)` 返回 `{ teamId, myRole, enabled, items }`，`getKnowledgeGraphSchema(kgId)` 返回含 `entityTypes`/`relationTypes` 的结构（`ui/src/api/knowledgeGraph.ts:128,188`）。**动手前先读该文件确认 items 元素字段名（id/name/description/mode）与 schema 的 relationTypes 字段名，下列代码按此假设写，字段名不一致时以类型为准调整。**

- [ ] **Step 1: 在面板组件加模板常量与预填函数**（放在组件外层或组件内，与现有 helper 同层级）：

```tsx
const KG_CYPHER_TEMPLATE_KEY = 'kg_cypher_query'

/**
 * kg_cypher_query：按选中图谱预填描述与配置（模型写对 Cypher 的第一喂养位）。
 */
async function prefillKgCypherQuery(
  teamId: number,
  kgId: number,
  setFields: (description: string, config: string) => void
): Promise<void> {
  const graphs = await getKnowledgeGraphs(teamId)
  const graph = graphs.items.find((g) => Number(g.id) === kgId)
  if (!graph) return
  let summary = ''
  try {
    const schema = await getKnowledgeGraphSchema(kgId)
    const entityNames = (schema.entityTypes ?? []).map((x) => x.name).filter(Boolean)
    const relationNames = (schema.relationTypes ?? []).map((x) => x.name).filter(Boolean)
    summary = `实体类型：${entityNames.join('、') || '（未定义）'}；关系类型：${relationNames.join('、') || '（未定义）'}。`
  } catch {
    summary = ''
  }
  const usage =
    graph.mode === 'connected'
      ? '接入图谱：节点使用原生 label 与关系类型，查询无需 $kgId 过滤。'
      : '托管图谱：节点标签为 KgNode（含 name/description 属性），所有 MATCH 必须带 {kgId: $kgId} 过滤，$kgId 由系统自动注入。'
  const description = `${graph.description || graph.name}。${summary}${usage}首次使用可传 {"Schema": true} 获取图谱结构。`
  setFields(description.slice(0, 255), JSON.stringify({ KgId: kgId, MaxRows: 200, TimeoutSeconds: 30 }, null, 2))
}
```

- [ ] **Step 2: 模板 Select 的 `onChange` 里加分支**（现有 `if (tp) form.setFieldValue('config', tp.configExample ?? '{}')` 之外追加）：

```tsx
                onChange={(v) => {
                  const tp = templates.find((x) => x.key === v)
                  if (tp) form.setFieldValue('config', tp.configExample ?? '{}')
                  if (v === KG_CYPHER_TEMPLATE_KEY) {
                    prefillKgCypherQuery(teamId, Number(graphValueRef.current), (description, config) => {
                      form.setFieldValue('description', description)
                      form.setFieldValue('config', config)
                    }).catch(() => message.error(t('plugins.kgBindingLoadFailed')))
                  }
                }}
```

由于图谱选择先于/独立于模板选择，**实际实现建议**：模板为 `kg_cypher_query` 时在模板 Select 下方渲染一个「绑定知识图谱」Select（`Form.Item noStyle shouldUpdate` 或 `Form.Item` + `dependencies`），其 `onChange` 直接调 `prefillKgCypherQuery`——图谱变化与模板变化都触发预填，配置编辑器始终以预填值为起点可手改。图谱下拉数据在弹窗打开且模板命中时懒加载：`getKnowledgeGraphs(teamId)` → options `{ value: g.id, label: g.name }`，`enabled !== true` 时显示禁用提示。

- [ ] **Step 3: i18n 两个文件 `plugins` 段同步加键**

zh-CN `common.json`：

```json
    "kgBinding": "绑定知识图谱",
    "kgBindingPlaceholder": "选择本团队的图谱，自动生成工具描述与配置",
    "kgBindingLoadFailed": "图谱信息加载失败"
```

en-US `common.json`：

```json
    "kgBinding": "Bind knowledge graph",
    "kgBindingPlaceholder": "Pick a team graph to auto-fill tool description and config",
    "kgBindingLoadFailed": "Failed to load graph info"
```

- [ ] **Step 4: 前端验证**

Run: `cd ui && npm run typecheck && npm run lint`
Expected: 全绿

- [ ] **Step 5: Commit**

```bash
git add ui/src/pages/teams/plugins/TeamDynamicPluginPanel.tsx ui/src/i18n/locales/zh-CN/common.json ui/src/i18n/locales/en-US/common.json
git commit -m "feat(ui): 团队动态插件 kg_cypher_query 绑定图谱下拉与描述/配置预填"
```

---

### Task 8: E2E `local-dev/kg-text2cypher-e2e.mjs`（KT-S1~S10）

**Files:**
- Create: `local-dev/kg-text2cypher-e2e.mjs`
- Modify: `local-dev/dynamic-plugin-e2e.mjs`（补模板注册断言）

**运行约定**：复用 kg-e2e 骨架（BASE 默认 `http://127.0.0.1:5210`，root 登录 admin/abcd1234**56**，RSA 加密，KG 未开启/图库不可达时 `skip()` 退出码 0）。场景编号规范 `KT-S<n>`（与 bdd 场景一一对应）。建图谱内容（类型/节点/边）的 API 路径与 body **照抄 `kg-e2e.mjs` S4/S5/S7 段的 `api()` 调用**（`POST /api/knowledge-graph/{id}/entity-types`、`/relation-types`、`/nodes`、`/edges` 一族，以该脚本实际代码为准）；实例创建 `POST /api/team/{TID}/plugin/dynamic`，运行 `POST /api/team/{TID}/plugin/run`（body `{teamId, key, requestJson}`，响应 `PluginRunResult {success, error, dataJson}`）。

- [ ] **Step 1: 写脚本**。主体结构（骨架照 kg-e2e.mjs:1-80 抄 `BASE/check/skip/rsa/api` 与登录/建团/能力检测/探活；以下为场景断言部分，`data()` 为 `JSON.parse(run.json.dataJson)` 辅助）：

```js
  // ===== KT-S1 创建 kg_cypher_query 实例（绑定托管图）=====
  const instKey = 'kg_cypher_' + TS
  const save1 = await api('POST', `/api/team/${TID}/plugin/dynamic`, {
    token,
    body: { teamId: TID, instanceKey: instKey, templeteKey: 'kg_cypher_query', title: '查图插件', description: 'KT', config: JSON.stringify({ KgId: GRAPH_ID, MaxRows: 200, TimeoutSeconds: 30 }), classifyId: 0 },
  })
  check('KT-S1a 创建实例 200', save1.status === 200, `${save1.status} ${save1.text.slice(0, 120)}`)

  // ===== KT-S2 越团队 kgId 403 =====
  const team2 = await api('POST', '/api/team', { token, body: { name: 'kg-t2-' + TS } })
  const T2 = Number(team2.json?.value)
  const save2 = await api('POST', `/api/team/${T2}/plugin/dynamic`, {
    token,
    body: { teamId: T2, instanceKey: 'kg_bad_' + TS, templeteKey: 'kg_cypher_query', title: '越权', description: '', config: JSON.stringify({ KgId: GRAPH_ID }), classifyId: 0 },
  })
  check('KT-S2 越团队绑定 403', save2.status === 403, `${save2.status}`)

  // ===== KT-S3 schema 自描述 =====
  const run3 = await api('POST', `/api/team/${TID}/plugin/run`, { token, body: { teamId: TID, key: instKey, requestJson: JSON.stringify({ Schema: true }) } })
  const d3 = run3.json?.success ? JSON.parse(run3.json.dataJson) : null
  check('KT-S3 schema 成功且含实体类型与 $kgId 指引', run3.status === 200 && run3.json?.success === true && (d3?.EntityTypes?.length ?? d3?.entityTypes?.length ?? 0) >= 1 && JSON.stringify(d3).includes('$kgId'), run3.text.slice(0, 160))

  // ===== KT-S4 合法只读查询 =====
  const run4 = await api('POST', `/api/team/${TID}/plugin/run`, { token, body: { teamId: TID, key: instKey, requestJson: JSON.stringify({ Cypher: 'MATCH (n:KgNode {kgId: $kgId}) RETURN n.name AS name LIMIT 5' }) } })
  const d4 = run4.json?.success ? JSON.parse(run4.json.dataJson) : null
  const rowCount4 = d4 ? (d4.RowCount ?? d4.rowCount ?? 0) : -1
  check('KT-S4 合法查询返回行', run4.json?.success === true && rowCount4 >= 1, run4.text.slice(0, 160))

  // ===== KT-S5 缺 $kgId 被拒且文案教学 =====
  const run5 = await api('POST', `/api/team/${TID}/plugin/run`, { token, body: { teamId: TID, key: instKey, requestJson: JSON.stringify({ Cypher: 'MATCH (n:KgNode) RETURN n LIMIT 5' }) } })
  check('KT-S5 缺 $kgId 报错含指引', run5.json?.success === false && (run5.json?.error ?? '').includes('$kgId'), JSON.stringify(run5.json).slice(0, 160))

  // ===== KT-S6 写语句逐个被拒 =====
  for (const bad of ['CREATE (n:KgNode {kgId: $kgId, name: \'x\'}) RETURN n', 'MATCH (n:KgNode {kgId: $kgId}) DETACH DELETE n', 'MATCH (n:KgNode {kgId: $kgId}) SET n.name = \'x\' RETURN n', 'CALL db.labels() YIELD label RETURN label', 'MERGE (n:KgNode {kgId: $kgId, name: \'x\'}) RETURN n']) {
    const run6 = await api('POST', `/api/team/${TID}/plugin/run`, { token, body: { teamId: TID, key: instKey, requestJson: JSON.stringify({ Cypher: bad }) } })
    check(`KT-S6 拒绝: ${bad.slice(0, 24)}...`, run6.json?.success === false, JSON.stringify(run6.json).slice(0, 100))
  }

  // ===== KT-S7 行数截断（建 MaxRows=2 实例，图中节点数 ≥3）=====
  const instKey2 = 'kg_cypher_tr_' + TS
  await api('POST', `/api/team/${TID}/plugin/dynamic`, { token, body: { teamId: TID, instanceKey: instKey2, templeteKey: 'kg_cypher_query', title: '截断', description: '', config: JSON.stringify({ KgId: GRAPH_ID, MaxRows: 2 }), classifyId: 0 } })
  const run7 = await api('POST', `/api/team/${TID}/plugin/run`, { token, body: { teamId: TID, key: instKey2, requestJson: JSON.stringify({ Cypher: 'MATCH (n:KgNode {kgId: $kgId}) RETURN n.name AS name' }) } })
  const d7 = run7.json?.success ? JSON.parse(run7.json.dataJson) : null
  check('KT-S7 rows=2 且 truncated=true', run7.json?.success === true && (d7?.RowCount ?? 0) === 2 && (d7?.Truncated ?? d7?.truncated) === true, JSON.stringify(d7).slice(0, 120))

  // ===== KT-S8 超时（不可稳定构造慢查询则 SKIP）=====
  console.warn('SKIP | KT-S8 超时场景依赖慢查询，本地验证时可在 Memgraph 中注入 EXPAND 负载后手动验证')

  // ===== KT-S9 接入图实例（探活成功才执行）=====
  if (CONN) {
    const instKey3 = 'kg_cypher_conn_' + TS
    await api('POST', `/api/team/${TID}/plugin/dynamic`, { token, body: { teamId: TID, instanceKey: instKey3, templeteKey: 'kg_cypher_query', title: '接入', description: '', config: JSON.stringify({ KgId: CONN }), classifyId: 0 } })
    const run9 = await api('POST', `/api/team/${TID}/plugin/run`, { token, body: { teamId: TID, key: instKey3, requestJson: JSON.stringify({ Cypher: 'MATCH (n) RETURN count(n) AS c LIMIT 1' }) } })
    check('KT-S9 接入图查询无需 $kgId', run9.json?.success === true, run9.text.slice(0, 160))
  } else {
    console.warn('SKIP | KT-S9 依赖可达的外部 neo4j 库（connected 探活失败）')
  }

  // ===== KT-S10 无实例团队运行 404 =====
  const run10 = await api('POST', `/api/team/${T2}/plugin/run`, { token, body: { teamId: T2, key: instKey, requestJson: JSON.stringify({ Schema: true }) } })
  check('KT-S10 未绑定团队运行 404', run10.status === 404 || run10.json?.success === false, `${run10.status}`)
```

脚本结尾照 kg-e2e 惯例打印 `PASS/FAIL` 计数并 `process.exit(FAIL > 0 ? 1 : 0)`。**实例 key 与团队名带 TS 时间戳**；KT-S7 前确保图谱已建 ≥3 节点（kg-e2e S7 建点段照抄扩为 3 个）。清理段照 kg-e2e：删图、删实例（若有删除端点；无则留给唯一 key 时间戳隔离）。

- [ ] **Step 2: `dynamic-plugin-e2e.mjs` 补断言**（在模板注册/实例创建断言区追加）：

```js
  // DYN: kg_cypher_query 模板注册
  const kgTp = templates.find((x) => x.key === 'kg_cypher_query')
  check('DYN kg_cypher_query 模板已注册且为动态', Boolean(kgTp) && kgTp.isDynamic === true, JSON.stringify(templates.map((x) => x.key)))
```

（变量名按该脚本实际结构调整。）

- [ ] **Step 3: 运行**（需后端运行中 + Memgraph 可达 + KG_ENABLED=true）

Run: `node local-dev/kg-text2cypher-e2e.mjs && node local-dev/kg-e2e.mjs`
Expected: KT 全 PASS（KT-S8/S9 按条件 SKIP）；kg-e2e 既有场景不回归

- [ ] **Step 4: Commit**

```bash
git add local-dev/kg-text2cypher-e2e.mjs local-dev/dynamic-plugin-e2e.mjs
git commit -m "test(e2e): KT 知识图谱 Text2Cypher 插件 E2E（隔离/只读/截断/接入图）+ DYN 注册断言"
```

---

### Task 9: 文档四件套 + 登记 + 全量验证

**Files:**
- Modify: `docs/knowledgegraph/bdd.md`（补 KT 场景）、`docs/knowledgegraph/tdd.md`（补映射）、`docs/knowledgegraph/sdd.md`（消费端章节链接）、`docs/knowledgegraph/sop.md`（插件运维注记）
- Modify: `AGENTS.md`（验证命令清单）
- Modify: `docs/rounds-log.md`（轮次记录）
- Modify: `docs/superpowers/specs/2026-09-21-kg-text2cypher-plugin-design.md`（表名修正）

- [ ] **Step 1: 修正设计文档两处**——§6 表名 `kg_entity_type`/`kg_relation_type` 改为 `knowledge_graph_entity_type`/`knowledge_graph_relation_type`（实体表真名，见 `DatabaseContext.cs:125-129`）；§10.2 场景编号 `KT-01~10` 改为 `KT-S1~S10`（与 bdd `@<缩写>-S<n>` 规范一致）。

- [ ] **Step 2: 改 bdd/tdd/sdd/sop**。**改前先重读文件最新内容**（并行会话可能已更新）。bdd.md 在外部接口场景（KX）之后新增「Text2Cypher 插件消费」小节，逐条列出 @KT-S1~S10 的 Given/When/Then（内容按 Task 8 场景表展开）；tdd.md 补 KT 场景 ↔ `CypherReadOnlyGuardTests` 单测 + `kg-text2cypher-e2e.mjs` 的映射并注明执行结果；sdd.md「后续迭代方向」一节把「AI 抽取入图（审核流）、应用绑定 graph_ids 与图检索（GraphRAG）」保留，新增一行「✅ 已落地：Text2Cypher 查图插件（见 specs/2026-09-21 设计文档 + KT 场景）」；sop.md 补一段「kg_cypher_query 插件：只读保障三层（守卫/$kgId/截断）与排障（缺 $kgId 报错文案、实例绑定 403）」。

- [ ] **Step 3: AGENTS.md 验证命令清单**（wiki-recall-e2e 行之后）追加：

```bash
node local-dev/kg-text2cypher-e2e.mjs    # KT（知识图谱 Text2Cypher：实例绑定校验/schema 自描述/只读守卫/$kgId 隔离/行数截断/接入图；依赖 Memgraph）
```

- [ ] **Step 4: rounds-log.md 记录轮次**（闭环证据：构建/测试/E2E 结果、commit 清单、SKIP 项）。

- [ ] **Step 5: 全量验证**

```bash
dotnet build src/MoAI/MoAI.csproj                       # 0 error
dotnet test tests/MoAI.AIPlugin.Dynamic.Tests/MoAI.AIPlugin.Dynamic.Tests.csproj   # 全 PASS
cd ui && npm run typecheck && npm run lint && npm run test   # 全绿
node local-dev/kg-text2cypher-e2e.mjs                   # KT 全 PASS（条件 SKIP 标注原因）
node local-dev/kg-e2e.mjs && node local-dev/dynamic-plugin-e2e.mjs   # 不回归
```

- [ ] **Step 6: Commit**

```bash
git add docs/knowledgegraph docs/rounds-log.md AGENTS.md docs/superpowers/specs/2026-09-21-kg-text2cypher-plugin-design.md
git commit -m "docs(knowledgegraph): Text2Cypher 插件四件套场景与轮次登记 + 设计文档表名/编号修正"
```

---

## 自审记录（写计划后已核）

1. **Spec 覆盖**：§5 契约→Task 5；§6 摘要→Task 4；§7.1 守卫→Task 1/2；§7.2 $kgId→Task 4；§7.3 超时截断→Task 4；§7.4 权限链→Task 6（E2E KT-S2/S10）；§8 前端→Task 7；§9 错误→教学式文案遍布 Task 2/4；§10.2 E2E→Task 8；§10.3 文档→Task 9。无缺口。
2. **占位符扫描**：Task 8 中「照抄 kg-e2e.mjs S4/S5/S7 段」是指向仓库既有可运行代码的复制指令（非 TBD）；其余步骤均含完整代码/命令。
3. **类型一致性**：`KgCypherQueryResult(Columns, Rows, RowCount, Truncated)` ↔ 插件 Response 字段；`IKgCypherAccessService.ExecuteQueryAsync(kgId, cypher, params, maxRows, timeoutSeconds, ct)` ↔ 插件调用处；DTO record 名在 Task 3/4/5 一致；`KgCypherTemplateKey = "kg_cypher_query"` 与 `[AiPlugin(key: "kg_cypher_query")]` 一致。
