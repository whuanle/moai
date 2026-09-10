# 知识图谱模块（后端）实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在 MoAI 新增独立知识图谱后端模块 `src/knowledgegraph`，团队级可建多个图谱，支持 schema（实体类型/关系类型）、内置模板、节点与边的手动增删改查，图数据只存 Neo4j，schema/目录存 PostgreSQL。

**Architecture:** 三层 CQRS（`MoAI.KnowledgeGraph.Shared/Core/Api`）。PostgreSQL 存 `kg`/`kg_entity_type`/`kg_relation_type` 目录与 schema；Neo4j 存 `:KgNode`/`:KG_REL`，用节点属性 `kgId` 隔离多图谱、`entityTypeId`/`relationTypeId` 回查 schema 名称，避免动态标签迁移。连接与开关复用 `IKnowledgeGraphSettingsService`（`OPEN_NEO4J` 等）。

**Tech Stack:** .NET 10、Maomi 模块框架、MediatR、EF Core（PostgreSQL）、Neo4j.Driver、FluentValidation、xunit + Moq。

**关联真源：** [设计稿](../specs/2026-09-10-knowledge-graph-design.md) ｜ [CQRS 规范](../../cqrs-conventions.md) ｜ [设置 SDD](../../settings/sdd.md)

---

## 文件结构

**新增项目**
- `src/knowledgegraph/MoAI.KnowledgeGraph.Shared/` — Command/Query/Response/模板模型（根命名空间 `MoAI.KnowledgeGraph`）
- `src/knowledgegraph/MoAI.KnowledgeGraph.Core/` — Handler、`IKnowledgeGraphStore`、Neo4j 实现、`IKnowledgeGraphAuthorizer`
- `src/knowledgegraph/MoAI.KnowledgeGraph.Api/` — `KnowledgeGraphController`、`KnowledgeGraphApiModule`
- `tests/MoAI.KnowledgeGraph.Tests/` — 单元测试

**修改既有**
- `Directory.Packages.props` — 增加 `Neo4j.Driver` 版本
- `src/database/MoAI.Database.Shared/DatabaseContext.cs` — 增加 3 个 `DbSet`
- `src/database/MoAI.Database.Shared/Entities/` — 3 个实体
- `src/database/MoAI.Database.Postgres/Data/` — 3 个 Configuration
- `src/MoAI/MainModule.cs` — 注册 `KnowledgeGraphCoreModule`
- `src/MoAI/MoAI.csproj` — 引用 `MoAI.KnowledgeGraph.Core`
- `MoAI.sln` — 新增 3 个项目与 solution folder
- `asserts/knowledge_graph.sql` — 增量 DDL
- `docker-compose.yml` — 可选 `neo4j` 服务

**分层依赖单向：** `Shared` ← `Api` ← `Core`；`Core` 额外引用 `MoAI.Database.Shared`、`MoAI.Team.Shared`、`MoAI.Settings.Shared`、`MoAI.Infra.Configuration.Shared`。

---

## Task 1: 脚手架（项目 + 解决方案 + 引用）

**Files:**
- Create: `src/knowledgegraph/MoAI.KnowledgeGraph.Shared/MoAI.KnowledgeGraph.Shared.csproj`
- Create: `src/knowledgegraph/MoAI.KnowledgeGraph.Shared/KnowledgeGraphSharedModule.cs`
- Create: `src/knowledgegraph/MoAI.KnowledgeGraph.Api/MoAI.KnowledgeGraph.Api.csproj`
- Create: `src/knowledgegraph/MoAI.KnowledgeGraph.Api/KnowledgeGraphApiModule.cs`
- Create: `src/knowledgegraph/MoAI.KnowledgeGraph.Core/MoAI.KnowledgeGraph.Core.csproj`
- Create: `src/knowledgegraph/MoAI.KnowledgeGraph.Core/KnowledgeGraphCoreModule.cs`
- Modify: `Directory.Packages.props`、`src/MoAI/MainModule.cs`、`src/MoAI/MoAI.csproj`、`MoAI.sln`

- [ ] **Step 1: 增加 Neo4j 包版本**

在 `Directory.Packages.props` 的 `<ItemGroup>` 内，紧跟 `<PackageVersion Include="Npgsql" Version="10.0.3" />` 之后追加：

```xml
<PackageVersion Include="Neo4j.Driver" Version="5.28.0" />
```

- [ ] **Step 2: 创建 Shared 项目**

`src/knowledgegraph/MoAI.KnowledgeGraph.Shared/MoAI.KnowledgeGraph.Shared.csproj`：

```xml
<Project Sdk="Microsoft.NET.Sdk">

	<PropertyGroup>
		<OutputType>Library</OutputType>
		<RootNamespace>MoAI.KnowledgeGraph</RootNamespace>
	</PropertyGroup>

	<ItemGroup>
		<ProjectReference Include="..\..\infra\MoAI.Infra.Shared\MoAI.Infra.Shared.csproj" />
	</ItemGroup>

	<ItemGroup>
		<Folder Include="Commands\" />
		<Folder Include="Queries\" />
		<Folder Include="Queries\Responses\" />
		<Folder Include="Models\" />
	</ItemGroup>

</Project>
```

`src/knowledgegraph/MoAI.KnowledgeGraph.Shared/KnowledgeGraphSharedModule.cs`：

```csharp
using Maomi;

namespace MoAI.KnowledgeGraph;

/// <summary>
/// KnowledgeGraphSharedModule.
/// </summary>
public class KnowledgeGraphSharedModule : IModule
{
    /// <inheritdoc/>
    public void ConfigureServices(ServiceContext context)
    {
    }
}
```

- [ ] **Step 3: 创建 Api 项目**

`src/knowledgegraph/MoAI.KnowledgeGraph.Api/MoAI.KnowledgeGraph.Api.csproj`：

```xml
<Project Sdk="Microsoft.NET.Sdk">

	<PropertyGroup>
		<OutputType>Library</OutputType>
		<RootNamespace>MoAI.KnowledgeGraph</RootNamespace>
	</PropertyGroup>

	<ItemGroup>
		<ProjectReference Include="..\..\infra\MoAI.Infra.Api\MoAI.Infra.Api.csproj" />
		<ProjectReference Include="..\MoAI.KnowledgeGraph.Shared\MoAI.KnowledgeGraph.Shared.csproj" />
	</ItemGroup>

	<ItemGroup>
		<Folder Include="Controllers\" />
	</ItemGroup>

</Project>
```

`src/knowledgegraph/MoAI.KnowledgeGraph.Api/KnowledgeGraphApiModule.cs`：

```csharp
using Maomi;

namespace MoAI.KnowledgeGraph;

/// <summary>
/// KnowledgeGraphApiModule.
/// </summary>
public class KnowledgeGraphApiModule : IModule
{
    /// <inheritdoc/>
    public void ConfigureServices(ServiceContext context)
    {
    }
}
```

- [ ] **Step 4: 创建 Core 项目与模块**

`src/knowledgegraph/MoAI.KnowledgeGraph.Core/MoAI.KnowledgeGraph.Core.csproj`：

```xml
<Project Sdk="Microsoft.NET.Sdk">

	<PropertyGroup>
		<OutputType>Library</OutputType>
		<RootNamespace>MoAI.KnowledgeGraph</RootNamespace>
	</PropertyGroup>

	<ItemGroup>
		<ProjectReference Include="..\..\database\MoAI.Database.Shared\MoAI.Database.Shared.csproj" />
		<ProjectReference Include="..\..\infra\MoAI.Infra.Configuration.Shared\MoAI.Infra.Configuration.csproj" />
		<ProjectReference Include="..\..\settings\MoAI.Settings.Shared\MoAI.Settings.Shared.csproj" />
		<ProjectReference Include="..\..\team\MoAI.Team.Shared\MoAI.Team.Shared.csproj" />
		<ProjectReference Include="..\MoAI.KnowledgeGraph.Api\MoAI.KnowledgeGraph.Api.csproj" />
	</ItemGroup>

	<ItemGroup>
		<PackageReference Include="Neo4j.Driver" />
	</ItemGroup>

	<ItemGroup>
		<Folder Include="Handlers\" />
		<Folder Include="Services\" />
	</ItemGroup>

	<ItemGroup>
		<AssemblyAttribute Include="System.Runtime.CompilerServices.InternalsVisibleTo">
			<_Parameter1>MoAI.KnowledgeGraph.Tests</_Parameter1>
		</AssemblyAttribute>
	</ItemGroup>

</Project>
```

`src/knowledgegraph/MoAI.KnowledgeGraph.Core/KnowledgeGraphCoreModule.cs`：

```csharp
using Maomi;
using Microsoft.Extensions.DependencyInjection;
using MoAI.KnowledgeGraph.Services;

namespace MoAI.KnowledgeGraph;

/// <summary>
/// KnowledgeGraphCoreModule.
/// </summary>
[InjectModule<KnowledgeGraphSharedModule>]
[InjectModule<KnowledgeGraphApiModule>]
public class KnowledgeGraphCoreModule : IModule
{
    /// <inheritdoc/>
    public void ConfigureServices(ServiceContext context)
    {
        context.Services.AddSingleton<Neo4jDriverProvider>();
        context.Services.AddScoped<IKnowledgeGraphStore>(sp => sp.GetRequiredService<Neo4jKnowledgeGraphStore>());
        context.Services.AddScoped<IKnowledgeGraphAuthorizer>(sp => sp.GetRequiredService<KnowledgeGraphAuthorizer>());
    }
}
```

> `Neo4jKnowledgeGraphStore` 与 `KnowledgeGraphAuthorizer` 在本文件首次编译时尚未创建，Task 4/5 创建后才会通过编译。Step 5 完成后允许暂时注释掉未实现的两行 `AddScoped`，Task 4/5 再恢复。

- [ ] **Step 5: 注册宿主**

`src/MoAI/MainModule.cs`：`using MoAI.KnowledgeGraph;` 加到 using 区，并在 `[InjectModule<WikiCoreModule>]` 下一行追加：

```csharp
[InjectModule<KnowledgeGraphCoreModule>]
```

`src/MoAI/MoAI.csproj`：在 `Wiki` 的 ProjectReference 后追加：

```xml
<ProjectReference Include="..\knowledgegraph\MoAI.KnowledgeGraph.Core\MoAI.KnowledgeGraph.Core.csproj" />
```

- [ ] **Step 6: 加入解决方案**

在 `MoAI.sln` 的 `src` 下新增 solution folder 与 3 个项目（沿用可读 GUID 约定，避免冲突）：

```
Project("{2150E333-8FDC-42A3-9474-1A3956D46DE8}") = "knowledgegraph", "knowledgegraph", "{B2000001-0000-0000-0000-000000000000}"
EndProject
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "MoAI.KnowledgeGraph.Shared", "src\knowledgegraph\MoAI.KnowledgeGraph.Shared\MoAI.KnowledgeGraph.Shared.csproj", "{B2000002-0000-0000-0000-000000000000}"
EndProject
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "MoAI.KnowledgeGraph.Core", "src\knowledgegraph\MoAI.KnowledgeGraph.Core\MoAI.KnowledgeGraph.Core.csproj", "{B2000003-0000-0000-0000-000000000000}"
EndProject
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "MoAI.KnowledgeGraph.Api", "src\knowledgegraph\MoAI.KnowledgeGraph.Api\MoAI.KnowledgeGraph.Api.csproj", "{B2000004-0000-0000-0000-000000000000}"
EndProject
```

同时在 `GlobalSection(NestedProjects)` 加：

```
{B2000001-0000-0000-0000-000000000000} = {821C7B98-83D8-4164-9C89-4C6B27287D33}
{B2000002-0000-0000-0000-000000000000} = {B2000001-0000-0000-0000-000000000000}
{B2000003-0000-0000-0000-000000000000} = {B2000001-0000-0000-0000-000000000000}
{B2000004-0000-0000-0000-000000000000} = {B2000001-0000-0000-0000-000000000000}
```

并在 `GlobalSection(ProjectConfigurationPlatforms)` 为 3 个新 GUID 各补 4 行（Debug/Release × Any CPU；照抄 wiki 项目的四条，替换 GUID）。

- [ ] **Step 7: 构建**

Run: `dotnet build src/MoAI/MoAI.csproj`
Expected: 0 error（若 Core 模块引用了尚未创建的类，先把对应 `AddScoped` 注释）

- [ ] **Step 8: 提交**

```bash
git add Directory.Packages.props MoAI.sln src/MoAI/MainModule.cs src/MoAI/MoAI.csproj src/knowledgegraph
git commit -m "feat(knowledge-graph): 搭建后端模块脚手架"
```

---

## Task 2: 实体、Configuration、DbSet 与 DDL

**Files:**
- Create: `src/database/MoAI.Database.Shared/Entities/KnowledgeGraphEntity.cs`
- Create: `src/database/MoAI.Database.Shared/Entities/KnowledgeGraphEntityTypeEntity.cs`
- Create: `src/database/MoAI.Database.Shared/Entities/KnowledgeGraphRelationTypeEntity.cs`
- Create: `src/database/MoAI.Database.Postgres/Data/KnowledgeGraphConfiguration.cs`
- Create: `src/database/MoAI.Database.Postgres/Data/KnowledgeGraphEntityTypeConfiguration.cs`
- Create: `src/database/MoAI.Database.Postgres/Data/KnowledgeGraphRelationTypeConfiguration.cs`
- Modify: `src/database/MoAI.Database.Shared/DatabaseContext.cs`
- Create: `asserts/knowledge_graph.sql`

- [ ] **Step 1: 实体**

`KnowledgeGraphEntity.cs`（对齐 `WikiEntity`：`IsDeleted` 为 `long`，实现 `IFullAudited`）：

```csharp
using System;
using MoAI.Database.Audits;

namespace MoAI.Database.Entities;

/// <summary>
/// 知识图谱.
/// </summary>
public partial class KnowledgeGraphEntity : IFullAudited
{
    /// <summary>
    /// id.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 所属团队 id.
    /// </summary>
    public int TeamId { get; set; }

    /// <summary>
    /// 名称.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// 简介.
    /// </summary>
    public string Description { get; set; } = default!;

    /// <summary>
    /// 创建时使用的模板 key，null 表示自定义.
    /// </summary>
    public string? TemplateKey { get; set; }

    /// <summary>
    /// 创建人.
    /// </summary>
    public long CreateUserId { get; set; }

    /// <summary>
    /// 创建时间.
    /// </summary>
    public DateTimeOffset CreateTime { get; set; }

    /// <summary>
    /// 最后修改人.
    /// </summary>
    public long UpdateUserId { get; set; }

    /// <summary>
    /// 更新时间.
    /// </summary>
    public DateTimeOffset UpdateTime { get; set; }

    /// <summary>
    /// 软删除.
    /// </summary>
    public long IsDeleted { get; set; }
}
```

`KnowledgeGraphEntityTypeEntity.cs`：

```csharp
using System;
using MoAI.Database.Audits;

namespace MoAI.Database.Entities;

/// <summary>
/// 知识图谱实体类型（schema）.
/// </summary>
public partial class KnowledgeGraphEntityTypeEntity : IFullAudited
{
    /// <summary>
    /// id.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 所属图谱 id.
    /// </summary>
    public long KgId { get; set; }

    /// <summary>
    /// 类型名称.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// 颜色（展示用，如 #1677ff）.
    /// </summary>
    public string Color { get; set; } = default!;

    /// <summary>
    /// 描述.
    /// </summary>
    public string Description { get; set; } = default!;

    /// <summary>
    /// 排序.
    /// </summary>
    public int Sort { get; set; }

    /// <summary>
    /// 创建人.
    /// </summary>
    public long CreateUserId { get; set; }

    /// <summary>
    /// 创建时间.
    /// </summary>
    public DateTimeOffset CreateTime { get; set; }

    /// <summary>
    /// 最后修改人.
    /// </summary>
    public long UpdateUserId { get; set; }

    /// <summary>
    /// 更新时间.
    /// </summary>
    public DateTimeOffset UpdateTime { get; set; }

    /// <summary>
    /// 软删除.
    /// </summary>
    public long IsDeleted { get; set; }
}
```

`KnowledgeGraphRelationTypeEntity.cs`：

```csharp
using System;
using MoAI.Database.Audits;

namespace MoAI.Database.Entities;

/// <summary>
/// 知识图谱关系类型（schema）.
/// </summary>
public partial class KnowledgeGraphRelationTypeEntity : IFullAudited
{
    /// <summary>
    /// id.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 所属图谱 id.
    /// </summary>
    public long KgId { get; set; }

    /// <summary>
    /// 关系类型名称.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// 颜色（展示用）.
    /// </summary>
    public string Color { get; set; } = default!;

    /// <summary>
    /// 描述.
    /// </summary>
    public string Description { get; set; } = default!;

    /// <summary>
    /// 允许的起点实体类型 id，null=任意.
    /// </summary>
    public long? SourceTypeId { get; set; }

    /// <summary>
    /// 允许的终点实体类型 id，null=任意.
    /// </summary>
    public long? TargetTypeId { get; set; }

    /// <summary>
    /// 排序.
    /// </summary>
    public int Sort { get; set; }

    /// <summary>
    /// 创建人.
    /// </summary>
    public long CreateUserId { get; set; }

    /// <summary>
    /// 创建时间.
    /// </summary>
    public DateTimeOffset CreateTime { get; set; }

    /// <summary>
    /// 最后修改人.
    /// </summary>
    public long UpdateUserId { get; set; }

    /// <summary>
    /// 更新时间.
    /// </summary>
    public DateTimeOffset UpdateTime { get; set; }

    /// <summary>
    /// 软删除.
    /// </summary>
    public long IsDeleted { get; set; }
}
```

- [ ] **Step 2: Configuration（3 个，镜像 WikiConfiguration 写法）**

`src/database/MoAI.Database.Postgres/Data/KnowledgeGraphConfiguration.cs`：

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MoAI.Database.Entities;

namespace MoAI.Database;

/// <summary>
/// 知识图谱.
/// </summary>
internal partial class KnowledgeGraphConfiguration : IEntityTypeConfiguration<KnowledgeGraphEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<KnowledgeGraphEntity> builder)
    {
        builder.HasKey(e => e.Id).HasName("idx_kg_primary");
        builder.ToTable("kg", tb => tb.HasComment("知识图谱"));

        builder.Property(e => e.Id).HasComment("id").HasColumnName("id");
        builder.Property(e => e.TeamId).HasComment("所属团队 id").HasColumnName("team_id");
        builder.Property(e => e.Name).HasMaxLength(50).HasComment("名称").HasColumnName("name");
        builder.Property(e => e.Description).HasMaxLength(255).HasDefaultValueSql("''::character varying").HasComment("简介").HasColumnName("description");
        builder.Property(e => e.TemplateKey).HasMaxLength(50).HasComment("模板 key").HasColumnName("template_key");
        builder.Property(e => e.CreateTime).HasDefaultValueSql("timezone('utc'::text, now())").HasComment("创建时间").HasColumnName("create_time");
        builder.Property(e => e.CreateUserId).HasComment("创建人").HasColumnName("create_user_id");
        builder.Property(e => e.UpdateTime).HasDefaultValueSql("timezone('utc'::text, now())").HasComment("更新时间").HasColumnName("update_time");
        builder.Property(e => e.UpdateUserId).HasComment("最后修改人").HasColumnName("update_user_id");
        builder.Property(e => e.IsDeleted).HasDefaultValueSql("'0'::bigint").HasComment("软删除").HasColumnName("is_deleted");
    }
}
```

`KnowledgeGraphEntityTypeConfiguration.cs`：

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MoAI.Database.Entities;

namespace MoAI.Database;

/// <summary>
/// 知识图谱实体类型.
/// </summary>
internal partial class KnowledgeGraphEntityTypeConfiguration : IEntityTypeConfiguration<KnowledgeGraphEntityTypeEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<KnowledgeGraphEntityTypeEntity> builder)
    {
        builder.HasKey(e => e.Id).HasName("idx_kg_entity_type_primary");
        builder.ToTable("kg_entity_type", tb => tb.HasComment("知识图谱实体类型"));

        builder.Property(e => e.Id).HasComment("id").HasColumnName("id");
        builder.Property(e => e.KgId).HasComment("所属图谱 id").HasColumnName("kg_id");
        builder.Property(e => e.Name).HasMaxLength(50).HasComment("类型名称").HasColumnName("name");
        builder.Property(e => e.Color).HasMaxLength(20).HasDefaultValueSql("''::character varying").HasComment("颜色").HasColumnName("color");
        builder.Property(e => e.Description).HasMaxLength(255).HasDefaultValueSql("''::character varying").HasComment("描述").HasColumnName("description");
        builder.Property(e => e.Sort).HasComment("排序").HasColumnName("sort");
        builder.Property(e => e.CreateTime).HasDefaultValueSql("timezone('utc'::text, now())").HasComment("创建时间").HasColumnName("create_time");
        builder.Property(e => e.CreateUserId).HasComment("创建人").HasColumnName("create_user_id");
        builder.Property(e => e.UpdateTime).HasDefaultValueSql("timezone('utc'::text, now())").HasComment("更新时间").HasColumnName("update_time");
        builder.Property(e => e.UpdateUserId).HasComment("最后修改人").HasColumnName("update_user_id");
        builder.Property(e => e.IsDeleted).HasDefaultValueSql("'0'::bigint").HasComment("软删除").HasColumnName("is_deleted");
    }
}
```

`KnowledgeGraphRelationTypeConfiguration.cs`：

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MoAI.Database.Entities;

namespace MoAI.Database;

/// <summary>
/// 知识图谱关系类型.
/// </summary>
internal partial class KnowledgeGraphRelationTypeConfiguration : IEntityTypeConfiguration<KnowledgeGraphRelationTypeEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<KnowledgeGraphRelationTypeEntity> builder)
    {
        builder.HasKey(e => e.Id).HasName("idx_kg_relation_type_primary");
        builder.ToTable("kg_relation_type", tb => tb.HasComment("知识图谱关系类型"));

        builder.Property(e => e.Id).HasComment("id").HasColumnName("id");
        builder.Property(e => e.KgId).HasComment("所属图谱 id").HasColumnName("kg_id");
        builder.Property(e => e.Name).HasMaxLength(50).HasComment("关系类型名称").HasColumnName("name");
        builder.Property(e => e.Color).HasMaxLength(20).HasDefaultValueSql("''::character varying").HasComment("颜色").HasColumnName("color");
        builder.Property(e => e.Description).HasMaxLength(255).HasDefaultValueSql("''::character varying").HasComment("描述").HasColumnName("description");
        builder.Property(e => e.SourceTypeId).HasComment("起点类型 id").HasColumnName("source_type_id");
        builder.Property(e => e.TargetTypeId).HasComment("终点类型 id").HasColumnName("target_type_id");
        builder.Property(e => e.Sort).HasComment("排序").HasColumnName("sort");
        builder.Property(e => e.CreateTime).HasDefaultValueSql("timezone('utc'::text, now())").HasComment("创建时间").HasColumnName("create_time");
        builder.Property(e => e.CreateUserId).HasComment("创建人").HasColumnName("create_user_id");
        builder.Property(e => e.UpdateTime).HasDefaultValueSql("timezone('utc'::text, now())").HasComment("更新时间").HasColumnName("update_time");
        builder.Property(e => e.UpdateUserId).HasComment("最后修改人").HasColumnName("update_user_id");
        builder.Property(e => e.IsDeleted).HasDefaultValueSql("'0'::bigint").HasComment("软删除").HasColumnName("is_deleted");
    }
}
```

- [ ] **Step 3: DbSet**

在 `src/database/MoAI.Database.Shared/DatabaseContext.cs` 的 `Wikis` 之后追加：

```csharp
    /// <summary>
    /// 知识图谱.
    /// </summary>
    public virtual DbSet<KnowledgeGraphEntity> KnowledgeGraphs { get; set; }

    /// <summary>
    /// 知识图谱实体类型.
    /// </summary>
    public virtual DbSet<KnowledgeGraphEntityTypeEntity> KnowledgeGraphEntityTypes { get; set; }

    /// <summary>
    /// 知识图谱关系类型.
    /// </summary>
    public virtual DbSet<KnowledgeGraphRelationTypeEntity> KnowledgeGraphRelationTypes { get; set; }
```

- [ ] **Step 4: DDL**

`asserts/knowledge_graph.sql`（已有库手动执行；空库由 `EnsureCreated` 生成）：

```sql
-- 知识图谱模块增量 DDL：kg / kg_entity_type / kg_relation_type
CREATE TABLE IF NOT EXISTS public.kg (
    id              bigint GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
    team_id         integer      NOT NULL,
    name            varchar(50)  NOT NULL,
    description     varchar(255) NOT NULL DEFAULT '',
    template_key    varchar(50)  NULL,
    is_deleted      bigint       NOT NULL DEFAULT 0,
    create_user_id  bigint       NOT NULL DEFAULT 0,
    create_time     timestamptz  NOT NULL DEFAULT timezone('utc'::text, now()),
    update_user_id  bigint       NOT NULL DEFAULT 0,
    update_time     timestamptz  NOT NULL DEFAULT timezone('utc'::text, now())
);

CREATE INDEX IF NOT EXISTS idx_kg_team_id ON public.kg (team_id);
CREATE UNIQUE INDEX IF NOT EXISTS idx_kg_team_name_live_uindex ON public.kg (team_id, name) WHERE is_deleted = 0;
ALTER TABLE public.kg OWNER to postgres;

CREATE TABLE IF NOT EXISTS public.kg_entity_type (
    id              bigint GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
    kg_id           bigint       NOT NULL,
    name            varchar(50)  NOT NULL,
    color           varchar(20)  NOT NULL DEFAULT '',
    description     varchar(255) NOT NULL DEFAULT '',
    sort            integer      NOT NULL DEFAULT 0,
    is_deleted      bigint       NOT NULL DEFAULT 0,
    create_user_id  bigint       NOT NULL DEFAULT 0,
    create_time     timestamptz  NOT NULL DEFAULT timezone('utc'::text, now()),
    update_user_id  bigint       NOT NULL DEFAULT 0,
    update_time     timestamptz  NOT NULL DEFAULT timezone('utc'::text, now())
);

CREATE INDEX IF NOT EXISTS idx_kg_entity_type_kg_id ON public.kg_entity_type (kg_id);
CREATE UNIQUE INDEX IF NOT EXISTS idx_kg_entity_type_name_live_uindex ON public.kg_entity_type (kg_id, name) WHERE is_deleted = 0;
ALTER TABLE public.kg_entity_type OWNER to postgres;

CREATE TABLE IF NOT EXISTS public.kg_relation_type (
    id              bigint GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
    kg_id           bigint       NOT NULL,
    name            varchar(50)  NOT NULL,
    color           varchar(20)  NOT NULL DEFAULT '',
    description     varchar(255) NOT NULL DEFAULT '',
    source_type_id  bigint       NULL,
    target_type_id  bigint       NULL,
    sort            integer      NOT NULL DEFAULT 0,
    is_deleted      bigint       NOT NULL DEFAULT 0,
    create_user_id  bigint       NOT NULL DEFAULT 0,
    create_time     timestamptz  NOT NULL DEFAULT timezone('utc'::text, now()),
    update_user_id  bigint       NOT NULL DEFAULT 0,
    update_time     timestamptz  NOT NULL DEFAULT timezone('utc'::text, now())
);

CREATE INDEX IF NOT EXISTS idx_kg_relation_type_kg_id ON public.kg_relation_type (kg_id);
CREATE UNIQUE INDEX IF NOT EXISTS idx_kg_relation_type_name_live_uindex ON public.kg_relation_type (kg_id, name) WHERE is_deleted = 0;
ALTER TABLE public.kg_relation_type OWNER to postgres;
```

- [ ] **Step 5: 构建**

Run: `dotnet build src/MoAI/MoAI.csproj`
Expected: 0 error

- [ ] **Step 6: 提交**

```bash
git add src/database asserts/knowledge_graph.sql
git commit -m "feat(knowledge-graph): 新增 kg 目录与 schema 表"
```

---

## Task 3: 模板目录（Shared）

**Files:**
- Create: `src/knowledgegraph/MoAI.KnowledgeGraph.Shared/Models/KnowledgeGraphTemplate.cs`
- Create: `src/knowledgegraph/MoAI.KnowledgeGraph.Shared/Models/KnowledgeGraphTemplates.cs`
- Test: `tests/MoAI.KnowledgeGraph.Tests/KnowledgeGraphTemplatesTests.cs`

- [ ] **Step 1: 写失败测试**

`tests/MoAI.KnowledgeGraph.Tests/KnowledgeGraphTemplatesTests.cs`：

```csharp
using MoAI.KnowledgeGraph.Models;
using Xunit;

namespace MoAI.KnowledgeGraph.Tests;

public class KnowledgeGraphTemplatesTests
{
    [Fact]
    public void All_ContainsBlankAndOps()
    {
        Assert.Contains(KnowledgeGraphTemplates.All, x => x.Key == KnowledgeGraphTemplates.BlankKey);
        Assert.Contains(KnowledgeGraphTemplates.All, x => x.Key == "ops");
    }

    [Fact]
    public void Find_ReturnsTemplateOrNull()
    {
        var ops = KnowledgeGraphTemplates.Find("ops");
        Assert.NotNull(ops);
        Assert.Contains("服务", ops!.EntityTypes);
        Assert.Contains(ops.RelationTypes, x => x.Name == "维护" && x.SourceType == "人员" && x.TargetType == "服务");
        Assert.Null(KnowledgeGraphTemplates.Find("not-exist"));
        Assert.Null(KnowledgeGraphTemplates.Find(null));
    }
}
```

- [ ] **Step 2: 运行测试确认失败**

Run: `dotnet test tests/MoAI.KnowledgeGraph.Tests/MoAI.KnowledgeGraph.Tests.csproj`
Expected: FAIL（命名空间/类型不存在，项目还未创建则先按 Task 11 建测试项目；也可先建项目再跑）

- [ ] **Step 3: 实现**

`Models/KnowledgeGraphTemplate.cs`：

```csharp
namespace MoAI.KnowledgeGraph.Models;

/// <summary>
/// 知识图谱模板.
/// </summary>
public class KnowledgeGraphTemplate
{
    /// <summary>
    /// 模板 key.
    /// </summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>
    /// 名称.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 描述.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// 预置实体类型名称.
    /// </summary>
    public IReadOnlyList<string> EntityTypes { get; init; } = Array.Empty<string>();

    /// <summary>
    /// 预置关系类型.
    /// </summary>
    public IReadOnlyList<KnowledgeGraphTemplateRelation> RelationTypes { get; init; } = Array.Empty<KnowledgeGraphTemplateRelation>();
}

/// <summary>
/// 模板中的关系类型.
/// </summary>
public class KnowledgeGraphTemplateRelation
{
    /// <summary>
    /// 关系名称.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 起点实体类型名称，null=任意.
    /// </summary>
    public string? SourceType { get; init; }

    /// <summary>
    /// 终点实体类型名称，null=任意.
    /// </summary>
    public string? TargetType { get; init; }
}
```

`Models/KnowledgeGraphTemplates.cs`：

```csharp
namespace MoAI.KnowledgeGraph.Models;

/// <summary>
/// 内置知识图谱模板目录（只读）.
/// </summary>
public static class KnowledgeGraphTemplates
{
    /// <summary>
    /// 空白模板 key.
    /// </summary>
    public const string BlankKey = "blank";

    /// <summary>
    /// 全部模板.
    /// </summary>
    public static readonly IReadOnlyList<KnowledgeGraphTemplate> All = new List<KnowledgeGraphTemplate>
    {
        new KnowledgeGraphTemplate
        {
            Key = BlankKey,
            Name = "空白 / 自定义",
            Description = "不预置类型，自行定义实体与关系",
        },
        new KnowledgeGraphTemplate
        {
            Key = "ops",
            Name = "运维服务",
            Description = "服务、人员、项目，维护与依赖",
            EntityTypes = new[] { "服务", "人员", "项目" },
            RelationTypes = new[]
            {
                new KnowledgeGraphTemplateRelation { Name = "维护", SourceType = "人员", TargetType = "服务" },
                new KnowledgeGraphTemplateRelation { Name = "依赖", SourceType = "项目", TargetType = "服务" },
            },
        },
        new KnowledgeGraphTemplate
        {
            Key = "org",
            Name = "组织人脉",
            Description = "人员、部门、公司，任职与隶属",
            EntityTypes = new[] { "人员", "部门", "公司" },
            RelationTypes = new[]
            {
                new KnowledgeGraphTemplateRelation { Name = "任职", SourceType = "人员", TargetType = "部门" },
                new KnowledgeGraphTemplateRelation { Name = "隶属", SourceType = "部门", TargetType = "公司" },
            },
        },
        new KnowledgeGraphTemplate
        {
            Key = "event",
            Name = "事件脉络",
            Description = "事件、时间、人物，参与与先后",
            EntityTypes = new[] { "事件", "时间", "人物" },
            RelationTypes = new[]
            {
                new KnowledgeGraphTemplateRelation { Name = "参与", SourceType = "人物", TargetType = "事件" },
                new KnowledgeGraphTemplateRelation { Name = "先后", SourceType = "事件", TargetType = "事件" },
            },
        },
    };

    /// <summary>
    /// 按 key 查找模板.
    /// </summary>
    /// <param name="key">模板 key.</param>
    /// <returns>模板或 null.</returns>
    public static KnowledgeGraphTemplate? Find(string? key)
        => string.IsNullOrWhiteSpace(key) ? null : All.FirstOrDefault(x => x.Key == key);
}
```

- [ ] **Step 4: 运行测试确认通过**

Run: `dotnet test tests/MoAI.KnowledgeGraph.Tests/MoAI.KnowledgeGraph.Tests.csproj --filter KnowledgeGraphTemplatesTests`
Expected: PASS

- [ ] **Step 5: 提交**

```bash
git add src/knowledgegraph/MoAI.KnowledgeGraph.Shared/Models tests/MoAI.KnowledgeGraph.Tests
git commit -m "feat(knowledge-graph): 内置模板目录"
```

---

## Task 4: Neo4j 驱动与存储层

**Files:**
- Create: `src/knowledgegraph/MoAI.KnowledgeGraph.Core/Services/KnowledgeGraphNodeRecord.cs`
- Create: `src/knowledgegraph/MoAI.KnowledgeGraph.Core/Services/KnowledgeGraphEdgeRecord.cs`
- Create: `src/knowledgegraph/MoAI.KnowledgeGraph.Core/Services/IKnowledgeGraphStore.cs`
- Create: `src/knowledgegraph/MoAI.KnowledgeGraph.Core/Services/Neo4jDriverProvider.cs`
- Create: `src/knowledgegraph/MoAI.KnowledgeGraph.Core/Services/Neo4jKnowledgeGraphStore.cs`

- [ ] **Step 1: 记录类型**

`KnowledgeGraphNodeRecord.cs`：

```csharp
namespace MoAI.KnowledgeGraph.Services;

/// <summary>
/// Neo4j 节点记录.
/// </summary>
/// <param name="Id">节点 id（guid 字符串）.</param>
/// <param name="KgId">所属图谱 id.</param>
/// <param name="EntityTypeId">实体类型 id.</param>
/// <param name="Name">名称.</param>
/// <param name="Description">描述.</param>
public sealed record KnowledgeGraphNodeRecord(string Id, long KgId, long EntityTypeId, string Name, string Description);
```

`KnowledgeGraphEdgeRecord.cs`：

```csharp
namespace MoAI.KnowledgeGraph.Services;

/// <summary>
/// Neo4j 边记录.
/// </summary>
/// <param name="Id">边 id（guid 字符串）.</param>
/// <param name="KgId">所属图谱 id.</param>
/// <param name="RelationTypeId">关系类型 id.</param>
/// <param name="SourceNodeId">起点节点 id.</param>
/// <param name="TargetNodeId">终点节点 id.</param>
public sealed record KnowledgeGraphEdgeRecord(string Id, long KgId, long RelationTypeId, string SourceNodeId, string TargetNodeId);
```

- [ ] **Step 2: 存储接口**

`IKnowledgeGraphStore.cs`：

```csharp
namespace MoAI.KnowledgeGraph.Services;

/// <summary>
/// 知识图谱图数据存储（Neo4j）.
/// </summary>
public interface IKnowledgeGraphStore
{
    /// <summary>
    /// 统计某实体类型下的节点数.
    /// </summary>
    Task<int> CountNodesByEntityTypeAsync(long kgId, long entityTypeId, CancellationToken cancellationToken);

    /// <summary>
    /// 统计某关系类型下的边数.
    /// </summary>
    Task<int> CountEdgesByRelationTypeAsync(long kgId, long relationTypeId, CancellationToken cancellationToken);

    /// <summary>
    /// 创建节点.
    /// </summary>
    Task<KnowledgeGraphNodeRecord> CreateNodeAsync(long kgId, long entityTypeId, string name, string description, CancellationToken cancellationToken);

    /// <summary>
    /// 更新节点.
    /// </summary>
    Task UpdateNodeAsync(long kgId, string nodeId, long entityTypeId, string name, string description, CancellationToken cancellationToken);

    /// <summary>
    /// 删除节点（连带其边），返回是否删除成功.
    /// </summary>
    Task<bool> DeleteNodeAsync(long kgId, string nodeId, CancellationToken cancellationToken);

    /// <summary>
    /// 获取节点.
    /// </summary>
    Task<KnowledgeGraphNodeRecord?> GetNodeAsync(long kgId, string nodeId, CancellationToken cancellationToken);

    /// <summary>
    /// 分页查询节点.
    /// </summary>
    Task<(IReadOnlyList<KnowledgeGraphNodeRecord> Items, long Total)> ListNodesAsync(long kgId, long? entityTypeId, string? keyword, int pageNo, int pageSize, CancellationToken cancellationToken);

    /// <summary>
    /// 创建边.
    /// </summary>
    Task<KnowledgeGraphEdgeRecord> CreateEdgeAsync(long kgId, long relationTypeId, string sourceNodeId, string targetNodeId, CancellationToken cancellationToken);

    /// <summary>
    /// 更新边，返回是否更新成功.
    /// </summary>
    Task<bool> UpdateEdgeAsync(long kgId, string edgeId, long relationTypeId, CancellationToken cancellationToken);

    /// <summary>
    /// 删除边，返回是否删除成功.
    /// </summary>
    Task<bool> DeleteEdgeAsync(long kgId, string edgeId, CancellationToken cancellationToken);

    /// <summary>
    /// 获取边.
    /// </summary>
    Task<KnowledgeGraphEdgeRecord?> GetEdgeAsync(long kgId, string edgeId, CancellationToken cancellationToken);

    /// <summary>
    /// 分页查询边.
    /// </summary>
    Task<(IReadOnlyList<KnowledgeGraphEdgeRecord> Items, long Total)> ListEdgesAsync(long kgId, long? relationTypeId, string? nodeId, int pageNo, int pageSize, CancellationToken cancellationToken);

    /// <summary>
    /// 清空图谱在图库中的所有节点与边.
    /// </summary>
    Task PurgeGraphAsync(long kgId, CancellationToken cancellationToken);
}
```

- [ ] **Step 3: 驱动提供者**

`Neo4jDriverProvider.cs`（单例；读设置、按连接串缓存 driver、连接变化时重建）：

```csharp
using Neo4j.Driver;
using MoAI.Infra.Exceptions;
using MoAI.Settings.Services;

namespace MoAI.KnowledgeGraph.Services;

/// <summary>
/// Neo4j 驱动提供者：按当前设置缓存并复用 driver，连接信息变化时重建.
/// </summary>
public sealed class Neo4jDriverProvider : IAsyncDisposable
{
    private const string ConstraintCypher = "CREATE CONSTRAINT kg_node_id_unique IF NOT EXISTS FOR (n:KgNode) REQUIRE n.id IS UNIQUE";
    private const string IndexCypher = "CREATE INDEX kg_node_kg_name IF NOT EXISTS FOR (n:KgNode) ON (n.kgId, n.name)";

    private readonly IKnowledgeGraphSettingsService _settingsService;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private IDriver? _driver;
    private string? _connectionKey;
    private bool _initialized;

    /// <summary>
    /// Initializes a new instance of the <see cref="Neo4jDriverProvider"/> class.
    /// </summary>
    /// <param name="settingsService">知识图谱设置读取服务.</param>
    public Neo4jDriverProvider(IKnowledgeGraphSettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    /// <summary>
    /// 获取当前驱动，未开启能力时抛 409.
    /// </summary>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>返回 <see cref="IDriver"/>.</returns>
    public async Task<IDriver> GetDriverAsync(CancellationToken cancellationToken)
    {
        var settings = await _settingsService.GetAsync(cancellationToken);
        if (!settings.Enabled || string.IsNullOrWhiteSpace(settings.Uri))
        {
            throw new BusinessException("未开启知识图谱能力，请先在系统设置中配置 Neo4j.") { StatusCode = 409 };
        }

        var key = $"{settings.Uri}|{settings.Username}|{settings.Password}";
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_driver == null || _connectionKey != key)
            {
                if (_driver != null)
                {
                    await _driver.DisposeAsync();
                }

                _driver = GraphDatabase.Driver(new Uri(settings.Uri), AuthTokens.Basic(settings.Username, settings.Password));
                _connectionKey = key;
                _initialized = false;
            }

            return _driver;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// 幂等初始化约束与索引（每个进程一次）.
    /// </summary>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>异步任务.</returns>
    public async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_initialized)
        {
            return;
        }

        var driver = await GetDriverAsync(cancellationToken);
        await using var session = driver.AsyncSession();
        await session.ExecuteWriteAsync(async tx =>
        {
            await tx.RunAsync(ConstraintCypher);
            await tx.RunAsync(IndexCypher);
        });

        _initialized = true;
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_driver != null)
        {
            await _driver.DisposeAsync();
        }

        _lock.Dispose();
    }
}
```

- [ ] **Step 4: 存储实现**

`Neo4jKnowledgeGraphStore.cs`：

```csharp
using MoAI.Infra.Exceptions;
using Neo4j.Driver;

namespace MoAI.KnowledgeGraph.Services;

/// <summary>
/// 基于 Neo4j 的知识图谱存储.
/// </summary>
public sealed class Neo4jKnowledgeGraphStore : IKnowledgeGraphStore
{
    private const string NodeReturn = "n.id AS id, n.kgId AS kgId, n.entityTypeId AS entityTypeId, n.name AS name, n.description AS description";
    private const string EdgeReturn = "r.id AS id, r.kgId AS kgId, r.relationTypeId AS relationTypeId, s.id AS sourceNodeId, t.id AS targetNodeId";

    private readonly Neo4jDriverProvider _provider;

    /// <summary>
    /// Initializes a new instance of the <see cref="Neo4jKnowledgeGraphStore"/> class.
    /// </summary>
    /// <param name="provider">Neo4j 驱动提供者.</param>
    public Neo4jKnowledgeGraphStore(Neo4jDriverProvider provider)
    {
        _provider = provider;
    }

    /// <inheritdoc/>
    public async Task<int> CountNodesByEntityTypeAsync(long kgId, long entityTypeId, CancellationToken cancellationToken)
    {
        var cursor = await RunAsync(
            "MATCH (n:KgNode {kgId: $kgId, entityTypeId: $entityTypeId}) RETURN count(n) AS c",
            new { kgId, entityTypeId },
            cancellationToken);
        var records = await cursor.ToListAsync();
        return records[0]["c"].As<long>().ConvertToInt();
    }

    /// <inheritdoc/>
    public async Task<int> CountEdgesByRelationTypeAsync(long kgId, long relationTypeId, CancellationToken cancellationToken)
    {
        var cursor = await RunAsync(
            "MATCH ()-[r:KG_REL {kgId: $kgId, relationTypeId: $relationTypeId}]->() RETURN count(r) AS c",
            new { kgId, relationTypeId },
            cancellationToken);
        var records = await cursor.ToListAsync();
        return records[0]["c"].As<long>().ConvertToInt();
    }

    /// <inheritdoc/>
    public async Task<KnowledgeGraphNodeRecord> CreateNodeAsync(long kgId, long entityTypeId, string name, string description, CancellationToken cancellationToken)
    {
        await _provider.EnsureInitializedAsync(cancellationToken);
        var id = Guid.CreateVersion7().ToString();
        await WriteAsync(
            "CREATE (n:KgNode {id: $id, kgId: $kgId, entityTypeId: $entityTypeId, name: $name, description: $description})",
            new { id, kgId, entityTypeId, name, description },
            cancellationToken);
        return new KnowledgeGraphNodeRecord(id, kgId, entityTypeId, name, description);
    }

    /// <inheritdoc/>
    public async Task UpdateNodeAsync(long kgId, string nodeId, long entityTypeId, string name, string description, CancellationToken cancellationToken)
    {
        await WriteAsync(
            "MATCH (n:KgNode {kgId: $kgId, id: $id}) SET n.entityTypeId = $entityTypeId, n.name = $name, n.description = $description",
            new { kgId, id = nodeId, entityTypeId, name, description },
            cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteNodeAsync(long kgId, string nodeId, CancellationToken cancellationToken)
    {
        var cursor = await WriteCursorAsync(
            "MATCH (n:KgNode {kgId: $kgId, id: $id}) DETACH DELETE n RETURN count(*) AS c",
            new { kgId, id = nodeId },
            cancellationToken);
        var records = await cursor.ToListAsync();
        return records.Count > 0 && records[0]["c"].As<long>() > 0;
    }

    /// <inheritdoc/>
    public async Task<KnowledgeGraphNodeRecord?> GetNodeAsync(long kgId, string nodeId, CancellationToken cancellationToken)
    {
        var cursor = await RunAsync(
            $"MATCH (n:KgNode {{kgId: $kgId, id: $id}}) RETURN {NodeReturn}",
            new { kgId, id = nodeId },
            cancellationToken);
        var records = await cursor.ToListAsync();
        return records.Count == 0 ? null : MapNode(records[0]);
    }

    /// <inheritdoc/>
    public async Task<(IReadOnlyList<KnowledgeGraphNodeRecord> Items, long Total)> ListNodesAsync(long kgId, long? entityTypeId, string? keyword, int pageNo, int pageSize, CancellationToken cancellationToken)
    {
        const string where = "WHERE ($entityTypeId IS NULL OR n.entityTypeId = $entityTypeId) AND ($keyword IS NULL OR toLower(n.name) CONTAINS toLower($keyword))";
        var parameters = new { kgId, entityTypeId, keyword = string.IsNullOrWhiteSpace(keyword) ? null : keyword, skip = (pageNo - 1) * pageSize, limit = pageSize };

        var countCursor = await RunAsync($"MATCH (n:KgNode {{kgId: $kgId}}) {where} RETURN count(n) AS c", parameters, cancellationToken);
        var countRecords = await countCursor.ToListAsync();
        var total = countRecords[0]["c"].As<long>();

        var cursor = await RunAsync(
            $"MATCH (n:KgNode {{kgId: $kgId}}) {where} RETURN {NodeReturn} ORDER BY n.name SKIP $skip LIMIT $limit",
            parameters,
            cancellationToken);
        var records = await cursor.ToListAsync();
        return (records.Select(MapNode).ToList(), total);
    }

    /// <inheritdoc/>
    public async Task<KnowledgeGraphEdgeRecord> CreateEdgeAsync(long kgId, long relationTypeId, string sourceNodeId, string targetNodeId, CancellationToken cancellationToken)
    {
        await _provider.EnsureInitializedAsync(cancellationToken);
        var id = Guid.CreateVersion7().ToString();
        var cursor = await WriteCursorAsync(
            "MATCH (s:KgNode {kgId: $kgId, id: $sourceNodeId}) " +
            "MATCH (t:KgNode {kgId: $kgId, id: $targetNodeId}) " +
            "CREATE (s)-[r:KG_REL {id: $id, kgId: $kgId, relationTypeId: $relationTypeId}]->(t) " +
            "RETURN r.id AS id",
            new { id, kgId, relationTypeId, sourceNodeId, targetNodeId },
            cancellationToken);
        var records = await cursor.ToListAsync();
        if (records.Count == 0)
        {
            throw new BusinessException("起点或终点节点不存在.") { StatusCode = 400 };
        }

        return new KnowledgeGraphEdgeRecord(id, kgId, relationTypeId, sourceNodeId, targetNodeId);
    }

    /// <inheritdoc/>
    public async Task<bool> UpdateEdgeAsync(long kgId, string edgeId, long relationTypeId, CancellationToken cancellationToken)
    {
        var cursor = await WriteCursorAsync(
            "MATCH ()-[r:KG_REL {kgId: $kgId, id: $id}]->() SET r.relationTypeId = $relationTypeId RETURN r.id AS id",
            new { kgId, id = edgeId, relationTypeId },
            cancellationToken);
        var records = await cursor.ToListAsync();
        return records.Count > 0;
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteEdgeAsync(long kgId, string edgeId, CancellationToken cancellationToken)
    {
        var cursor = await WriteCursorAsync(
            "MATCH ()-[r:KG_REL {kgId: $kgId, id: $id}]->() DELETE r RETURN count(*) AS c",
            new { kgId, id = edgeId },
            cancellationToken);
        var records = await cursor.ToListAsync();
        return records.Count > 0 && records[0]["c"].As<long>() > 0;
    }

    /// <inheritdoc/>
    public async Task<KnowledgeGraphEdgeRecord?> GetEdgeAsync(long kgId, string edgeId, CancellationToken cancellationToken)
    {
        var cursor = await RunAsync(
            $"MATCH (s:KgNode {{kgId: $kgId}})-[r:KG_REL {{kgId: $kgId, id: $id}}]->(t:KgNode {{kgId: $kgId}}) RETURN {EdgeReturn}",
            new { kgId, id = edgeId },
            cancellationToken);
        var records = await cursor.ToListAsync();
        return records.Count == 0 ? null : MapEdge(records[0]);
    }

    /// <inheritdoc/>
    public async Task<(IReadOnlyList<KnowledgeGraphEdgeRecord> Items, long Total)> ListEdgesAsync(long kgId, long? relationTypeId, string? nodeId, int pageNo, int pageSize, CancellationToken cancellationToken)
    {
        const string where = "WHERE ($relationTypeId IS NULL OR r.relationTypeId = $relationTypeId) AND ($nodeId IS NULL OR s.id = $nodeId OR t.id = $nodeId)";
        var parameters = new { kgId, relationTypeId, nodeId = string.IsNullOrWhiteSpace(nodeId) ? null : nodeId, skip = (pageNo - 1) * pageSize, limit = pageSize };

        var countCursor = await RunAsync($"MATCH (s:KgNode {{kgId: $kgId}})-[r:KG_REL {{kgId: $kgId}}]->(t:KgNode {{kgId: $kgId}}) {where} RETURN count(r) AS c", parameters, cancellationToken);
        var countRecords = await countCursor.ToListAsync();
        var total = countRecords[0]["c"].As<long>();

        var cursor = await RunAsync(
            $"MATCH (s:KgNode {{kgId: $kgId}})-[r:KG_REL {{kgId: $kgId}}]->(t:KgNode {{kgId: $kgId}}) {where} RETURN {EdgeReturn} ORDER BY r.id SKIP $skip LIMIT $limit",
            parameters,
            cancellationToken);
        var records = await cursor.ToListAsync();
        return (records.Select(MapEdge).ToList(), total);
    }

    /// <inheritdoc/>
    public async Task PurgeGraphAsync(long kgId, CancellationToken cancellationToken)
    {
        await WriteAsync("MATCH (n:KgNode {kgId: $kgId}) DETACH DELETE n", new { kgId }, cancellationToken);
    }

    private static KnowledgeGraphNodeRecord MapNode(IRecord record)
        => new(record["id"].As<string>(), record["kgId"].As<long>(), record["entityTypeId"].As<long>(), record["name"].As<string>(), record["description"].As<string>() ?? string.Empty);

    private static KnowledgeGraphEdgeRecord MapEdge(IRecord record)
        => new(record["id"].As<string>(), record["kgId"].As<long>(), record["relationTypeId"].As<long>(), record["sourceNodeId"].As<string>(), record["targetNodeId"].As<string>());

    private async Task<IResultCursor> RunAsync(string cypher, object parameters, CancellationToken cancellationToken)
    {
        var driver = await _provider.GetDriverAsync(cancellationToken);
        var session = driver.AsyncSession();
        return await session.RunAsync(cypher, parameters);
    }

    private async Task WriteAsync(string cypher, object parameters, CancellationToken cancellationToken)
    {
        var driver = await _provider.GetDriverAsync(cancellationToken);
        await using var session = driver.AsyncSession();
        await session.ExecuteWriteAsync(tx => tx.RunAsync(cypher, parameters));
    }

    private async Task<IResultCursor> WriteCursorAsync(string cypher, object parameters, CancellationToken cancellationToken)
    {
        var driver = await _provider.GetDriverAsync(cancellationToken);
        await using var session = driver.AsyncSession();
        var result = new List<IRecord>();
        await session.ExecuteWriteAsync(async tx =>
        {
            var cursor = await tx.RunAsync(cypher, parameters);
            result.AddRange(await cursor.ToListAsync());
        });
        var driver2 = driver;
        return new MaterializedCursor(result);
    }

    private sealed class MaterializedCursor : IResultCursor
    {
        private readonly IReadOnlyList<IRecord> _records;
        public MaterializedCursor(IReadOnlyList<IRecord> records) => _records = records;
        public Task<IResultSummary> ConsumeAsync() => Task.FromResult<IResultSummary>(null!);
        public IAsyncEnumerable<IRecord> ConsumeAsync(bool fetchSize) => throw new NotSupportedException();
        public Task<IRecord> SingleAsync() { return Task.FromResult(_records.Single()); }
        public Task<IRecord?> SingleOrDefaultAsync() { return Task.FromResult(_records.SingleOrDefault()); }
        public Task<IRecord> PeekAsync() => throw new NotSupportedException();
        public Task<IRecord?> FetchAsync() => throw new NotSupportedException();
        public Task<IResultSummary> ToListAsync() => throw new NotSupportedException();
        public IAsyncEnumerable<IRecord> ToListAsync(Action<IRecord> action) => throw new NotSupportedException();
        public IAsyncEnumerable<IRecord> ToListAsync(int fetchSize, Action<IRecord> action) => throw new NotSupportedException();
        public Task<IResultSummary> ToListAsync(Action<IReadOnlyList<IRecord>> action) => throw new NotSupportedException();
        public Task<IResultSummary> ToListAsync(int fetchSize, Action<IReadOnlyList<IRecord>> action) => throw new NotSupportedException();
        public Task<bool> FetchAsync(int fetchSize) => throw new NotSupportedException();
        public Task<IResultSummary> ConsumeAsync(Action<IRecord> action) => throw new NotSupportedException();
        public Task<IResultSummary> ConsumeAsync(int fetchSize, Action<IRecord> action) => throw new NotSupportedException();
    }
}
```

> 说明：`WriteCursorAsync` 需要回读 `RETURN` 值，而 Neo4j.Driver 的 `IResultCursor` 不能在 session 释放后继续读取，因此用一个内部 `MaterializedCursor` 承载已物化的记录。`IResultCursor` 成员较多，若编译报错可按接口定义补齐；更简做法是把 `Delete*`/`UpdateEdge`/`CreateEdge` 写为 `ExecuteWriteAsync` 内直接读 `cursor.ToListAsync()` 并返回 int/bool，避免实现 `IResultCursor`。**采用后者更稳：见下方替代实现。**

替代实现（推荐，去掉 `MaterializedCursor`，把“写并读取”合并进一个方法内部完成）：

```csharp
    private async Task<List<IRecord>> WriteReadAsync(string cypher, object parameters, CancellationToken cancellationToken)
    {
        var driver = await _provider.GetDriverAsync(cancellationToken);
        await using var session = driver.AsyncSession();
        var records = new List<IRecord>();
        await session.ExecuteWriteAsync(async tx =>
        {
            var cursor = await tx.RunAsync(cypher, parameters);
            records.AddRange(await cursor.ToListAsync());
        });
        return records;
    }
```

用 `WriteReadAsync` 重写：`DeleteNodeAsync` → `records.Count > 0 && records[0]["c"].As<long>() > 0`；`CreateEdgeAsync` → 记录为空时抛 400；`UpdateEdgeAsync`/`DeleteEdgeAsync` 同理。删除上面 `WriteCursorAsync` 与 `MaterializedCursor`，`int` 转换直接用 `(int)records[0]["c"].As<long>()`。

- [ ] **Step 5: 构建**

Run: `dotnet build src/MoAI/MoAI.csproj`
Expected: 0 error

- [ ] **Step 6: 提交**

```bash
git add src/knowledgegraph/MoAI.KnowledgeGraph.Core
git commit -m "feat(knowledge-graph): Neo4j 驱动与图存储封装"
```

---

## Task 5: 授权助手 + 图谱 CRUD

**Files:**
- Create: `.../Core/Services/IKnowledgeGraphAuthorizer.cs`
- Create: `.../Core/Services/KnowledgeGraphAuthorizer.cs`
- Create: `.../Shared/Commands/*`、`.../Shared/Queries/*`、`.../Shared/Queries/Responses/*`（见下）
- Create: `.../Core/Handlers/*`（见下）
- Test: `tests/MoAI.KnowledgeGraph.Tests/CreateKnowledgeGraphCommandHandlerTests.cs`

- [ ] **Step 1: 授权助手**

`IKnowledgeGraphAuthorizer.cs`：

```csharp
using MoAI.Database.Entities;
using MoAI.Database.Enums;

namespace MoAI.KnowledgeGraph.Services;

/// <summary>
/// 知识图谱权限判定.
/// </summary>
public interface IKnowledgeGraphAuthorizer
{
    /// <summary>
    /// 校验当前用户对团队的访问权限，adminOnly 时要求 Admin+.
    /// </summary>
    Task<TeamRole> RequireTeamRoleAsync(long teamId, bool adminOnly, CancellationToken cancellationToken);

    /// <summary>
    /// 校验当前用户对图谱的访问权限，返回图谱与角色.
    /// </summary>
    Task<(KnowledgeGraphEntity Graph, TeamRole Role)> AuthorizeAsync(long kgId, bool adminOnly, CancellationToken cancellationToken);
}
```

`KnowledgeGraphAuthorizer.cs`：

```csharp
using Maomi;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Services;
using MoAI.Team.Services;

namespace MoAI.KnowledgeGraph.Services;

/// <summary>
/// 知识图谱权限判定实现.
/// </summary>
[InjectOnScoped]
public class KnowledgeGraphAuthorizer : IKnowledgeGraphAuthorizer
{
    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;
    private readonly IUserContextProvider _userContextProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="KnowledgeGraphAuthorizer"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    /// <param name="userContextProvider">用户上下文提供者.</param>
    public KnowledgeGraphAuthorizer(DatabaseContext databaseContext, ITeamService teamService, IUserContextProvider userContextProvider)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
        _userContextProvider = userContextProvider;
    }

    /// <inheritdoc/>
    public async Task<TeamRole> RequireTeamRoleAsync(long teamId, bool adminOnly, CancellationToken cancellationToken)
    {
        var userId = _userContextProvider.GetUserContext().UserId;
        var role = await _teamService.GetMyRoleAsync(teamId, userId, cancellationToken);
        if (role == null)
        {
            throw new BusinessException("团队不存在或你不是团队成员.") { StatusCode = 404 };
        }

        if (adminOnly && role == TeamRole.Member)
        {
            throw new BusinessException("只有团队管理员可以执行该操作.") { StatusCode = 403 };
        }

        return role.Value;
    }

    /// <inheritdoc/>
    public async Task<(KnowledgeGraphEntity Graph, TeamRole Role)> AuthorizeAsync(long kgId, bool adminOnly, CancellationToken cancellationToken)
    {
        var graph = await _databaseContext.KnowledgeGraphs.FirstOrDefaultAsync(x => x.Id == kgId, cancellationToken);
        if (graph == null)
        {
            throw new BusinessException("知识图谱不存在.") { StatusCode = 404 };
        }

        var role = await RequireTeamRoleAsync(graph.TeamId, adminOnly, cancellationToken);
        return (graph, role);
    }
}
```

- [ ] **Step 2: 图谱命令/查询/响应（Shared）**

`Commands/CreateKnowledgeGraphCommand.cs`：

```csharp
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.KnowledgeGraph.Commands;

/// <summary>
/// 创建知识图谱，需要团队 Admin 及以上角色.
/// </summary>
public class CreateKnowledgeGraphCommand : IRequest<SimpleLong>, IModelValidator<CreateKnowledgeGraphCommand>
{
    /// <summary>
    /// 所属团队 id.
    /// </summary>
    public long TeamId { get; init; }

    /// <summary>
    /// 名称.
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// 简介.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// 模板 key，null=自定义.
    /// </summary>
    public string? TemplateKey { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<CreateKnowledgeGraphCommand> validate)
    {
        validate.RuleFor(x => x.TeamId).GreaterThan(0).WithMessage("团队 id 不正确.");
        validate.RuleFor(x => x.Name).NotEmpty().WithMessage("名称不能为空.").MaximumLength(50).WithMessage("名称最长 50 个字符.");
        validate.RuleFor(x => x.Description).MaximumLength(255).WithMessage("简介最长 255 个字符.");
    }
}
```

`Commands/UpdateKnowledgeGraphCommand.cs`：

```csharp
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.KnowledgeGraph.Commands;

/// <summary>
/// 更新知识图谱，需要团队 Admin 及以上角色.
/// </summary>
public class UpdateKnowledgeGraphCommand : IRequest<EmptyCommandResponse>, IModelValidator<UpdateKnowledgeGraphCommand>
{
    /// <summary>
    /// 图谱 id.
    /// </summary>
    public long KgId { get; init; }

    /// <summary>
    /// 名称.
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// 简介.
    /// </summary>
    public string? Description { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<UpdateKnowledgeGraphCommand> validate)
    {
        validate.RuleFor(x => x.KgId).GreaterThan(0).WithMessage("图谱 id 不正确.");
        validate.RuleFor(x => x.Name).NotEmpty().WithMessage("名称不能为空.").MaximumLength(50).WithMessage("名称最长 50 个字符.");
        validate.RuleFor(x => x.Description).MaximumLength(255).WithMessage("简介最长 255 个字符.");
    }
}
```

`Commands/DeleteKnowledgeGraphCommand.cs`：

```csharp
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.KnowledgeGraph.Commands;

/// <summary>
/// 删除知识图谱，需要团队 Admin 及以上角色.
/// </summary>
public class DeleteKnowledgeGraphCommand : IRequest<EmptyCommandResponse>
{
    /// <summary>
    /// 图谱 id.
    /// </summary>
    public long KgId { get; init; }
}
```

`Queries/QueryKnowledgeGraphsCommand.cs`：

```csharp
using MediatR;
using MoAI.KnowledgeGraph.Queries.Responses;

namespace MoAI.KnowledgeGraph.Queries;

/// <summary>
/// 查询团队下的知识图谱列表.
/// </summary>
public class QueryKnowledgeGraphsCommand : IRequest<QueryKnowledgeGraphsCommandResponse>
{
    /// <summary>
    /// 团队 id.
    /// </summary>
    public long TeamId { get; init; }
}
```

`Queries/QueryKnowledgeGraphCommand.cs`：

```csharp
using MediatR;
using MoAI.KnowledgeGraph.Queries.Responses;

namespace MoAI.KnowledgeGraph.Queries;

/// <summary>
/// 查询知识图谱详情.
/// </summary>
public class QueryKnowledgeGraphCommand : IRequest<QueryKnowledgeGraphCommandResponse>
{
    /// <summary>
    /// 图谱 id.
    /// </summary>
    public long KgId { get; init; }
}
```

`Queries/QueryKnowledgeGraphTemplatesCommand.cs`：

```csharp
using MediatR;
using MoAI.KnowledgeGraph.Queries.Responses;

namespace MoAI.KnowledgeGraph.Queries;

/// <summary>
/// 查询内置模板目录.
/// </summary>
public class QueryKnowledgeGraphTemplatesCommand : IRequest<QueryKnowledgeGraphTemplatesCommandResponse>
{
}
```

响应对象（`Queries/Responses/`）：

```csharp
// KnowledgeGraphItem.cs
namespace MoAI.KnowledgeGraph.Queries.Responses;

/// <summary>
/// 图谱列表项.
/// </summary>
public class KnowledgeGraphItem
{
    /// <summary>
    /// 图谱 id.
    /// </summary>
    public long KgId { get; init; }

    /// <summary>
    /// 团队 id.
    /// </summary>
    public long TeamId { get; init; }

    /// <summary>
    /// 名称.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 简介.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// 模板 key.
    /// </summary>
    public string? TemplateKey { get; init; }

    /// <summary>
    /// 创建时间.
    /// </summary>
    public DateTimeOffset CreateTime { get; init; }
}
```

```csharp
// QueryKnowledgeGraphsCommandResponse.cs
namespace MoAI.KnowledgeGraph.Queries.Responses;

/// <summary>
/// 图谱列表响应.
/// </summary>
public class QueryKnowledgeGraphsCommandResponse
{
    /// <summary>
    /// 团队 id.
    /// </summary>
    public long TeamId { get; init; }

    /// <summary>
    /// 我的角色（0=Member 1=Admin 2=Owner）.
    /// </summary>
    public int MyRole { get; init; }

    /// <summary>
    /// 能力是否开启.
    /// </summary>
    public bool Enabled { get; init; }

    /// <summary>
    /// 列表.
    /// </summary>
    public List<KnowledgeGraphItem> Items { get; init; } = new();
}
```

```csharp
// QueryKnowledgeGraphCommandResponse.cs
namespace MoAI.KnowledgeGraph.Queries.Responses;

/// <summary>
/// 图谱详情响应.
/// </summary>
public class QueryKnowledgeGraphCommandResponse
{
    /// <summary>
    /// 图谱 id.
    /// </summary>
    public long KgId { get; init; }

    /// <summary>
    /// 团队 id.
    /// </summary>
    public long TeamId { get; init; }

    /// <summary>
    /// 名称.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 简介.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// 模板 key.
    /// </summary>
    public string? TemplateKey { get; init; }

    /// <summary>
    /// 我的角色.
    /// </summary>
    public int MyRole { get; init; }

    /// <summary>
    /// 能力是否开启.
    /// </summary>
    public bool Enabled { get; init; }

    /// <summary>
    /// 创建时间.
    /// </summary>
    public DateTimeOffset CreateTime { get; init; }
}
```

```csharp
// KnowledgeGraphTemplateItem.cs
namespace MoAI.KnowledgeGraph.Queries.Responses;

/// <summary>
/// 模板列表项.
/// </summary>
public class KnowledgeGraphTemplateItem
{
    /// <summary>
    /// 模板 key.
    /// </summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>
    /// 名称.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 描述.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// 实体类型名称.
    /// </summary>
    public List<string> EntityTypes { get; init; } = new();

    /// <summary>
    /// 关系类型名称.
    /// </summary>
    public List<string> RelationTypes { get; init; } = new();
}
```

```csharp
// QueryKnowledgeGraphTemplatesCommandResponse.cs
namespace MoAI.KnowledgeGraph.Queries.Responses;

/// <summary>
/// 模板目录响应.
/// </summary>
public class QueryKnowledgeGraphTemplatesCommandResponse
{
    /// <summary>
    /// 模板列表.
    /// </summary>
    public List<KnowledgeGraphTemplateItem> Items { get; init; } = new();
}
```

- [ ] **Step 3: 图谱 Handler（Core）**

`Handlers/CreateKnowledgeGraphCommandHandler.cs`：

```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.KnowledgeGraph.Commands;
using MoAI.KnowledgeGraph.Models;
using MoAI.KnowledgeGraph.Services;
using MoAI.Settings.Services;

namespace MoAI.KnowledgeGraph.Handlers;

/// <summary>
/// <inheritdoc cref="CreateKnowledgeGraphCommand"/>
/// </summary>
public class CreateKnowledgeGraphCommandHandler : IRequestHandler<CreateKnowledgeGraphCommand, SimpleLong>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IKnowledgeGraphAuthorizer _authorizer;
    private readonly IKnowledgeGraphSettingsService _settingsService;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateKnowledgeGraphCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="authorizer">权限判定.</param>
    /// <param name="settingsService">知识图谱设置.</param>
    public CreateKnowledgeGraphCommandHandler(DatabaseContext databaseContext, IKnowledgeGraphAuthorizer authorizer, IKnowledgeGraphSettingsService settingsService)
    {
        _databaseContext = databaseContext;
        _authorizer = authorizer;
        _settingsService = settingsService;
    }

    /// <inheritdoc/>
    public async Task<SimpleLong> Handle(CreateKnowledgeGraphCommand request, CancellationToken cancellationToken)
    {
        await _authorizer.RequireTeamRoleAsync(request.TeamId, adminOnly: true, cancellationToken);

        var settings = await _settingsService.GetAsync(cancellationToken);
        if (!settings.Enabled)
        {
            throw new BusinessException("未开启知识图谱能力，请先在系统设置中配置 Neo4j.") { StatusCode = 409 };
        }

        KnowledgeGraphTemplate? template = null;
        if (!string.IsNullOrWhiteSpace(request.TemplateKey))
        {
            template = KnowledgeGraphTemplates.Find(request.TemplateKey)
                ?? throw new BusinessException("知识图谱模板不存在.") { StatusCode = 400 };
        }

        var nameExist = await _databaseContext.KnowledgeGraphs
            .AnyAsync(x => x.TeamId == request.TeamId && x.Name == request.Name, cancellationToken);
        if (nameExist)
        {
            throw new BusinessException("知识图谱名称已存在，请更换后重试.") { StatusCode = 409 };
        }

        var graph = new KnowledgeGraphEntity
        {
            TeamId = (int)request.TeamId,
            Name = request.Name,
            Description = request.Description ?? string.Empty,
            TemplateKey = request.TemplateKey,
        };
        _databaseContext.KnowledgeGraphs.Add(graph);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        if (template != null && template.EntityTypes.Count > 0)
        {
            var sort = 0;
            var typeNameToId = new Dictionary<string, long>(StringComparer.Ordinal);
            foreach (var typeName in template.EntityTypes)
            {
                var entityType = new KnowledgeGraphEntityTypeEntity
                {
                    KgId = graph.Id,
                    Name = typeName,
                    Description = string.Empty,
                    Color = string.Empty,
                    Sort = sort++,
                };
                _databaseContext.KnowledgeGraphEntityTypes.Add(entityType);
                typeNameToId[typeName] = entityType.Id;
            }

            // 先确保实体类型落库拿到自增 id
            await _databaseContext.SaveChangesAsync(cancellationToken);

            sort = 0;
            foreach (var relation in template.RelationTypes)
            {
                _databaseContext.KnowledgeGraphRelationTypes.Add(new KnowledgeGraphRelationTypeEntity
                {
                    KgId = graph.Id,
                    Name = relation.Name,
                    Description = string.Empty,
                    Color = string.Empty,
                    Sort = sort++,
                    SourceTypeId = relation.SourceType != null && typeNameToId.TryGetValue(relation.SourceType, out var sid) ? sid : null,
                    TargetTypeId = relation.TargetType != null && typeNameToId.TryGetValue(relation.TargetType, out var tid) ? tid : null,
                });
            }

            await _databaseContext.SaveChangesAsync(cancellationToken);
        }

        return new SimpleLong { Value = graph.Id };
    }
}
```

> 注意：实体类型自增 id 由 PostgreSQL identity 生成，`SaveChangesAsync` 后 EF 才会回填 `entityType.Id`；因此 `typeNameToId` 的取值必须放在“先落库”之后。上面已按此顺序写。

`Handlers/UpdateKnowledgeGraphCommandHandler.cs`：

```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.KnowledgeGraph.Commands;
using MoAI.KnowledgeGraph.Services;

namespace MoAI.KnowledgeGraph.Handlers;

/// <summary>
/// <inheritdoc cref="UpdateKnowledgeGraphCommand"/>
/// </summary>
public class UpdateKnowledgeGraphCommandHandler : IRequestHandler<UpdateKnowledgeGraphCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IKnowledgeGraphAuthorizer _authorizer;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateKnowledgeGraphCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="authorizer">权限判定.</param>
    public UpdateKnowledgeGraphCommandHandler(DatabaseContext databaseContext, IKnowledgeGraphAuthorizer authorizer)
    {
        _databaseContext = databaseContext;
        _authorizer = authorizer;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UpdateKnowledgeGraphCommand request, CancellationToken cancellationToken)
    {
        var (graph, _) = await _authorizer.AuthorizeAsync(request.KgId, adminOnly: true, cancellationToken);

        var nameExist = await _databaseContext.KnowledgeGraphs
            .AnyAsync(x => x.TeamId == graph.TeamId && x.Name == request.Name && x.Id != graph.Id, cancellationToken);
        if (nameExist)
        {
            throw new BusinessException("知识图谱名称已存在，请更换后重试.") { StatusCode = 409 };
        }

        graph.Name = request.Name;
        graph.Description = request.Description ?? string.Empty;
        await _databaseContext.SaveChangesAsync(cancellationToken);
        return EmptyCommandResponse.Default;
    }
}
```

`Handlers/DeleteKnowledgeGraphCommandHandler.cs`：

```csharp
using MediatR;
using MoAI.Database;
using MoAI.Infra.Models;
using MoAI.KnowledgeGraph.Commands;
using MoAI.KnowledgeGraph.Services;

namespace MoAI.KnowledgeGraph.Handlers;

/// <summary>
/// <inheritdoc cref="DeleteKnowledgeGraphCommand"/>
/// </summary>
public class DeleteKnowledgeGraphCommandHandler : IRequestHandler<DeleteKnowledgeGraphCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IKnowledgeGraphAuthorizer _authorizer;
    private readonly IKnowledgeGraphStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteKnowledgeGraphCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="authorizer">权限判定.</param>
    /// <param name="store">图存储.</param>
    public DeleteKnowledgeGraphCommandHandler(DatabaseContext databaseContext, IKnowledgeGraphAuthorizer authorizer, IKnowledgeGraphStore store)
    {
        _databaseContext = databaseContext;
        _authorizer = authorizer;
        _store = store;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(DeleteKnowledgeGraphCommand request, CancellationToken cancellationToken)
    {
        var (graph, _) = await _authorizer.AuthorizeAsync(request.KgId, adminOnly: true, cancellationToken);

        // 先清图数据，再软删目录；图库清理失败则整体失败，避免留下孤儿数据
        await _store.PurgeGraphAsync(graph.Id, cancellationToken);

        _databaseContext.KnowledgeGraphs.Remove(graph);
        await _databaseContext.SaveChangesAsync(cancellationToken);
        return EmptyCommandResponse.Default;
    }
}
```

`Handlers/QueryKnowledgeGraphsCommandHandler.cs`：

```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.KnowledgeGraph.Queries;
using MoAI.KnowledgeGraph.Queries.Responses;
using MoAI.KnowledgeGraph.Services;
using MoAI.Settings.Services;

namespace MoAI.KnowledgeGraph.Handlers;

/// <summary>
/// <inheritdoc cref="QueryKnowledgeGraphsCommand"/>
/// </summary>
public class QueryKnowledgeGraphsCommandHandler : IRequestHandler<QueryKnowledgeGraphsCommand, QueryKnowledgeGraphsCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IKnowledgeGraphAuthorizer _authorizer;
    private readonly IKnowledgeGraphSettingsService _settingsService;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryKnowledgeGraphsCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="authorizer">权限判定.</param>
    /// <param name="settingsService">知识图谱设置.</param>
    public QueryKnowledgeGraphsCommandHandler(DatabaseContext databaseContext, IKnowledgeGraphAuthorizer authorizer, IKnowledgeGraphSettingsService settingsService)
    {
        _databaseContext = databaseContext;
        _authorizer = authorizer;
        _settingsService = settingsService;
    }

    /// <inheritdoc/>
    public async Task<QueryKnowledgeGraphsCommandResponse> Handle(QueryKnowledgeGraphsCommand request, CancellationToken cancellationToken)
    {
        var role = await _authorizer.RequireTeamRoleAsync(request.TeamId, adminOnly: false, cancellationToken);
        var settings = await _settingsService.GetAsync(cancellationToken);

        var items = await _databaseContext.KnowledgeGraphs
            .Where(x => x.TeamId == request.TeamId)
            .OrderBy(x => x.Id)
            .Select(x => new KnowledgeGraphItem
            {
                KgId = x.Id,
                TeamId = x.TeamId,
                Name = x.Name,
                Description = x.Description,
                TemplateKey = x.TemplateKey,
                CreateTime = x.CreateTime,
            })
            .ToListAsync(cancellationToken);

        return new QueryKnowledgeGraphsCommandResponse
        {
            TeamId = request.TeamId,
            MyRole = (int)role,
            Enabled = settings.Enabled,
            Items = items,
        };
    }
}
```

`Handlers/QueryKnowledgeGraphCommandHandler.cs`：

```csharp
using MediatR;
using MoAI.KnowledgeGraph.Queries;
using MoAI.KnowledgeGraph.Queries.Responses;
using MoAI.KnowledgeGraph.Services;
using MoAI.Settings.Services;

namespace MoAI.KnowledgeGraph.Handlers;

/// <summary>
/// <inheritdoc cref="QueryKnowledgeGraphCommand"/>
/// </summary>
public class QueryKnowledgeGraphCommandHandler : IRequestHandler<QueryKnowledgeGraphCommand, QueryKnowledgeGraphCommandResponse>
{
    private readonly IKnowledgeGraphAuthorizer _authorizer;
    private readonly IKnowledgeGraphSettingsService _settingsService;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryKnowledgeGraphCommandHandler"/> class.
    /// </summary>
    /// <param name="authorizer">权限判定.</param>
    /// <param name="settingsService">知识图谱设置.</param>
    public QueryKnowledgeGraphCommandHandler(IKnowledgeGraphAuthorizer authorizer, IKnowledgeGraphSettingsService settingsService)
    {
        _authorizer = authorizer;
        _settingsService = settingsService;
    }

    /// <inheritdoc/>
    public async Task<QueryKnowledgeGraphCommandResponse> Handle(QueryKnowledgeGraphCommand request, CancellationToken cancellationToken)
    {
        var (graph, role) = await _authorizer.AuthorizeAsync(request.KgId, adminOnly: false, cancellationToken);
        var settings = await _settingsService.GetAsync(cancellationToken);

        return new QueryKnowledgeGraphCommandResponse
        {
            KgId = graph.Id,
            TeamId = graph.TeamId,
            Name = graph.Name,
            Description = graph.Description,
            TemplateKey = graph.TemplateKey,
            MyRole = (int)role,
            Enabled = settings.Enabled,
            CreateTime = graph.CreateTime,
        };
    }
}
```

`Handlers/QueryKnowledgeGraphTemplatesCommandHandler.cs`：

```csharp
using MediatR;
using MoAI.KnowledgeGraph.Models;
using MoAI.KnowledgeGraph.Queries;
using MoAI.KnowledgeGraph.Queries.Responses;

namespace MoAI.KnowledgeGraph.Handlers;

/// <summary>
/// <inheritdoc cref="QueryKnowledgeGraphTemplatesCommand"/>
/// </summary>
public class QueryKnowledgeGraphTemplatesCommandHandler : IRequestHandler<QueryKnowledgeGraphTemplatesCommand, QueryKnowledgeGraphTemplatesCommandResponse>
{
    /// <inheritdoc/>
    public Task<QueryKnowledgeGraphTemplatesCommandResponse> Handle(QueryKnowledgeGraphTemplatesCommand request, CancellationToken cancellationToken)
    {
        var items = KnowledgeGraphTemplates.All
            .Select(x => new KnowledgeGraphTemplateItem
            {
                Key = x.Key,
                Name = x.Name,
                Description = x.Description,
                EntityTypes = x.EntityTypes.ToList(),
                RelationTypes = x.RelationTypes.Select(r => r.Name).ToList(),
            })
            .ToList();

        return Task.FromResult(new QueryKnowledgeGraphTemplatesCommandResponse { Items = items });
    }
}
```

- [ ] **Step 4: 单元测试（图谱创建）**

先建测试项目（Task 11 有完整 csproj），`tests/MoAI.KnowledgeGraph.Tests/CreateKnowledgeGraphCommandHandlerTests.cs`：

```csharp
using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.KnowledgeGraph.Commands;
using MoAI.KnowledgeGraph.Handlers;
using MoAI.KnowledgeGraph.Services;
using MoAI.Settings.Models;
using MoAI.Settings.Services;
using Moq;
using Xunit;

namespace MoAI.KnowledgeGraph.Tests;

public class CreateKnowledgeGraphCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenEnabledAndAdmin_CreatesGraph()
    {
        using var db = CreateContext();
        var authorizer = new Mock<IKnowledgeGraphAuthorizer>();
        authorizer.Setup(x => x.RequireTeamRoleAsync(It.IsAny<long>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TeamRole.Admin);
        var settings = new Mock<IKnowledgeGraphSettingsService>();
        settings.Setup(x => x.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Neo4jKnowledgeGraphSettings { Enabled = true, Uri = "neo4j://localhost:7687" });

        var sut = new CreateKnowledgeGraphCommandHandler(db.Context, authorizer.Object, settings.Object);
        var result = await sut.Handle(new CreateKnowledgeGraphCommand { TeamId = 7, Name = "支付域" }, CancellationToken.None);

        Assert.True(result.Value > 0);
    }

    [Fact]
    public async Task Handle_WhenDisabled_Throws409()
    {
        using var db = CreateContext();
        var authorizer = new Mock<IKnowledgeGraphAuthorizer>();
        authorizer.Setup(x => x.RequireTeamRoleAsync(It.IsAny<long>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TeamRole.Admin);
        var settings = new Mock<IKnowledgeGraphSettingsService>();
        settings.Setup(x => x.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Neo4jKnowledgeGraphSettings { Enabled = false });

        var sut = new CreateKnowledgeGraphCommandHandler(db.Context, authorizer.Object, settings.Object);
        var ex = await Assert.ThrowsAsync<BusinessException>(() => sut.Handle(
            new CreateKnowledgeGraphCommand { TeamId = 7, Name = "支付域" }, CancellationToken.None));

        Assert.Equal(409, ex.StatusCode);
    }

    [Fact]
    public async Task Handle_WhenNameDuplicated_Throws409()
    {
        using var db = CreateContext();
        var authorizer = new Mock<IKnowledgeGraphAuthorizer>();
        authorizer.Setup(x => x.RequireTeamRoleAsync(It.IsAny<long>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TeamRole.Owner);
        var settings = new Mock<IKnowledgeGraphSettingsService>();
        settings.Setup(x => x.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Neo4jKnowledgeGraphSettings { Enabled = true, Uri = "neo4j://localhost:7687" });

        var sut = new CreateKnowledgeGraphCommandHandler(db.Context, authorizer.Object, settings.Object);
        await sut.Handle(new CreateKnowledgeGraphCommand { TeamId = 7, Name = "支付域" }, CancellationToken.None);
        var ex = await Assert.ThrowsAsync<BusinessException>(() => sut.Handle(
            new CreateKnowledgeGraphCommand { TeamId = 7, Name = "支付域" }, CancellationToken.None));

        Assert.Equal(409, ex.StatusCode);
    }

    private static SqliteScope CreateContext()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var services = new ServiceCollection();
        services.AddSingleton(Mock.Of<MoAI.Infra.Services.IIdProvider>());
        services.AddSingleton(Mock.Of<MoAI.Infra.Services.IUserContextProvider>());
        var provider = services.BuildServiceProvider();
        var options = new DbContextOptionsBuilder<DatabaseContext>().UseSqlite(connection).Options;
        var context = new TestDatabaseContext(options, provider);
        context.Database.EnsureCreated();
        return new SqliteScope(context, connection);
    }

    private sealed class TestDatabaseContext : DatabaseContext
    {
        public TestDatabaseContext(DbContextOptions options, IServiceProvider serviceProvider)
            : base(options, serviceProvider)
        {
        }

        protected override bool ShouldApplySeedData() => false;
    }

    private sealed class SqliteScope : IDisposable
    {
        public SqliteScope(DatabaseContext context, SqliteConnection connection)
        {
            Context = context;
            Connection = connection;
        }

        public DatabaseContext Context { get; }

        public SqliteConnection Connection { get; }

        public void Dispose()
        {
            Context.Dispose();
            Connection.Dispose();
        }
    }
}
```

- [ ] **Step 5: 运行测试**

Run: `dotnet test tests/MoAI.KnowledgeGraph.Tests/MoAI.KnowledgeGraph.Tests.csproj --filter CreateKnowledgeGraphCommandHandlerTests`
Expected: PASS

- [ ] **Step 6: 提交**

```bash
git add src/knowledgegraph tests/MoAI.KnowledgeGraph.Tests
git commit -m "feat(knowledge-graph): 图谱 CRUD 与授权助手"
```

---

## Task 6: Schema（实体类型 / 关系类型）与模板查询

**Files:**
- Create: `.../Shared/Commands/CreateKnowledgeGraphEntityTypeCommand.cs`、`UpdateKnowledgeGraphEntityTypeCommand.cs`、`DeleteKnowledgeGraphEntityTypeCommand.cs`
- Create: `.../Shared/Commands/CreateKnowledgeGraphRelationTypeCommand.cs`、`UpdateKnowledgeGraphRelationTypeCommand.cs`、`DeleteKnowledgeGraphRelationTypeCommand.cs`
- Create: `.../Shared/Queries/QueryKnowledgeGraphSchemaCommand.cs`
- Create: `.../Shared/Queries/Responses/KnowledgeGraphEntityTypeItem.cs`、`KnowledgeGraphRelationTypeItem.cs`、`QueryKnowledgeGraphSchemaCommandResponse.cs`
- Create: `.../Core/Handlers/*Schema*.cs`

**规则（所有 schema Handler 共用）：**
- 命令/查询均通过 `_authorizer.AuthorizeAsync(request.KgId, adminOnly: true/false, ct)` 校验
- 写 schema 前先确认能力开启：`_settingsService.GetAsync` 且 `Enabled==false` → 409
- 类型名在 `(kg_id, name)` 未删除范围内唯一，重复 → 409
- 删除实体类型前查 `_store.CountNodesByEntityTypeAsync`；>0 → 409「该实体类型下仍有节点，无法删除」
- 删除关系类型前查 `_store.CountEdgesByRelationTypeAsync`；>0 → 409「该关系类型下仍有关系，无法删除」
- 新增/修改关系类型时，若 `SourceTypeId`/`TargetTypeId` 非空，必须存在于该图谱实体类型中，否则 400

- [ ] **Step 1: 命令与响应**

`Commands/CreateKnowledgeGraphEntityTypeCommand.cs`：

```csharp
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.KnowledgeGraph.Commands;

/// <summary>
/// 新增实体类型，需要团队 Admin 及以上角色.
/// </summary>
public class CreateKnowledgeGraphEntityTypeCommand : IRequest<SimpleLong>, IModelValidator<CreateKnowledgeGraphEntityTypeCommand>
{
    /// <summary>
    /// 图谱 id.
    /// </summary>
    public long KgId { get; init; }

    /// <summary>
    /// 名称.
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// 颜色.
    /// </summary>
    public string? Color { get; init; }

    /// <summary>
    /// 描述.
    /// </summary>
    public string? Description { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<CreateKnowledgeGraphEntityTypeCommand> validate)
    {
        validate.RuleFor(x => x.KgId).GreaterThan(0).WithMessage("图谱 id 不正确.");
        validate.RuleFor(x => x.Name).NotEmpty().WithMessage("类型名称不能为空.").MaximumLength(50).WithMessage("类型名称最长 50 个字符.");
        validate.RuleFor(x => x.Color).MaximumLength(20).WithMessage("颜色最长 20 个字符.");
        validate.RuleFor(x => x.Description).MaximumLength(255).WithMessage("描述最长 255 个字符.");
    }
}
```

`Commands/UpdateKnowledgeGraphEntityTypeCommand.cs`：

```csharp
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.KnowledgeGraph.Commands;

/// <summary>
/// 修改实体类型，需要团队 Admin 及以上角色.
/// </summary>
public class UpdateKnowledgeGraphEntityTypeCommand : IRequest<EmptyCommandResponse>, IModelValidator<UpdateKnowledgeGraphEntityTypeCommand>
{
    /// <summary>
    /// 图谱 id.
    /// </summary>
    public long KgId { get; init; }

    /// <summary>
    /// 实体类型 id.
    /// </summary>
    public long EntityTypeId { get; init; }

    /// <summary>
    /// 名称.
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// 颜色.
    /// </summary>
    public string? Color { get; init; }

    /// <summary>
    /// 描述.
    /// </summary>
    public string? Description { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<UpdateKnowledgeGraphEntityTypeCommand> validate)
    {
        validate.RuleFor(x => x.KgId).GreaterThan(0).WithMessage("图谱 id 不正确.");
        validate.RuleFor(x => x.EntityTypeId).GreaterThan(0).WithMessage("实体类型 id 不正确.");
        validate.RuleFor(x => x.Name).NotEmpty().WithMessage("类型名称不能为空.").MaximumLength(50).WithMessage("类型名称最长 50 个字符.");
        validate.RuleFor(x => x.Color).MaximumLength(20).WithMessage("颜色最长 20 个字符.");
        validate.RuleFor(x => x.Description).MaximumLength(255).WithMessage("描述最长 255 个字符.");
    }
}
```

`Commands/DeleteKnowledgeGraphEntityTypeCommand.cs`：

```csharp
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.KnowledgeGraph.Commands;

/// <summary>
/// 删除实体类型，需要团队 Admin 及以上角色.
/// </summary>
public class DeleteKnowledgeGraphEntityTypeCommand : IRequest<EmptyCommandResponse>, IModelValidator<DeleteKnowledgeGraphEntityTypeCommand>
{
    /// <summary>
    /// 图谱 id.
    /// </summary>
    public long KgId { get; init; }

    /// <summary>
    /// 实体类型 id.
    /// </summary>
    public long EntityTypeId { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<DeleteKnowledgeGraphEntityTypeCommand> validate)
    {
        validate.RuleFor(x => x.KgId).GreaterThan(0).WithMessage("图谱 id 不正确.");
        validate.RuleFor(x => x.EntityTypeId).GreaterThan(0).WithMessage("实体类型 id 不正确.");
    }
}
```

`Commands/CreateKnowledgeGraphRelationTypeCommand.cs`：

```csharp
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.KnowledgeGraph.Commands;

/// <summary>
/// 新增关系类型，需要团队 Admin 及以上角色.
/// </summary>
public class CreateKnowledgeGraphRelationTypeCommand : IRequest<SimpleLong>, IModelValidator<CreateKnowledgeGraphRelationTypeCommand>
{
    /// <summary>
    /// 图谱 id.
    /// </summary>
    public long KgId { get; init; }

    /// <summary>
    /// 名称.
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// 颜色.
    /// </summary>
    public string? Color { get; init; }

    /// <summary>
    /// 描述.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// 起点实体类型 id，null=任意.
    /// </summary>
    public long? SourceTypeId { get; init; }

    /// <summary>
    /// 终点实体类型 id，null=任意.
    /// </summary>
    public long? TargetTypeId { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<CreateKnowledgeGraphRelationTypeCommand> validate)
    {
        validate.RuleFor(x => x.KgId).GreaterThan(0).WithMessage("图谱 id 不正确.");
        validate.RuleFor(x => x.Name).NotEmpty().WithMessage("关系名称不能为空.").MaximumLength(50).WithMessage("关系名称最长 50 个字符.");
        validate.RuleFor(x => x.Color).MaximumLength(20).WithMessage("颜色最长 20 个字符.");
        validate.RuleFor(x => x.Description).MaximumLength(255).WithMessage("描述最长 255 个字符.");
    }
}
```

`Commands/UpdateKnowledgeGraphRelationTypeCommand.cs`：

```csharp
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.KnowledgeGraph.Commands;

/// <summary>
/// 修改关系类型，需要团队 Admin 及以上角色.
/// </summary>
public class UpdateKnowledgeGraphRelationTypeCommand : IRequest<EmptyCommandResponse>, IModelValidator<UpdateKnowledgeGraphRelationTypeCommand>
{
    /// <summary>
    /// 图谱 id.
    /// </summary>
    public long KgId { get; init; }

    /// <summary>
    /// 关系类型 id.
    /// </summary>
    public long RelationTypeId { get; init; }

    /// <summary>
    /// 名称.
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// 颜色.
    /// </summary>
    public string? Color { get; init; }

    /// <summary>
    /// 描述.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// 起点实体类型 id，null=任意.
    /// </summary>
    public long? SourceTypeId { get; init; }

    /// <summary>
    /// 终点实体类型 id，null=任意.
    /// </summary>
    public long? TargetTypeId { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<UpdateKnowledgeGraphRelationTypeCommand> validate)
    {
        validate.RuleFor(x => x.KgId).GreaterThan(0).WithMessage("图谱 id 不正确.");
        validate.RuleFor(x => x.RelationTypeId).GreaterThan(0).WithMessage("关系类型 id 不正确.");
        validate.RuleFor(x => x.Name).NotEmpty().WithMessage("关系名称不能为空.").MaximumLength(50).WithMessage("关系名称最长 50 个字符.");
        validate.RuleFor(x => x.Color).MaximumLength(20).WithMessage("颜色最长 20 个字符.");
        validate.RuleFor(x => x.Description).MaximumLength(255).WithMessage("描述最长 255 个字符.");
    }
}
```

`Commands/DeleteKnowledgeGraphRelationTypeCommand.cs`：

```csharp
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.KnowledgeGraph.Commands;

/// <summary>
/// 删除关系类型，需要团队 Admin 及以上角色.
/// </summary>
public class DeleteKnowledgeGraphRelationTypeCommand : IRequest<EmptyCommandResponse>, IModelValidator<DeleteKnowledgeGraphRelationTypeCommand>
{
    /// <summary>
    /// 图谱 id.
    /// </summary>
    public long KgId { get; init; }

    /// <summary>
    /// 关系类型 id.
    /// </summary>
    public long RelationTypeId { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<DeleteKnowledgeGraphRelationTypeCommand> validate)
    {
        validate.RuleFor(x => x.KgId).GreaterThan(0).WithMessage("图谱 id 不正确.");
        validate.RuleFor(x => x.RelationTypeId).GreaterThan(0).WithMessage("关系类型 id 不正确.");
    }
}
```

`Queries/QueryKnowledgeGraphSchemaCommand.cs`：

```csharp
using MediatR;
using MoAI.KnowledgeGraph.Queries.Responses;

namespace MoAI.KnowledgeGraph.Queries;

/// <summary>
/// 查询图谱 schema（实体类型 + 关系类型）.
/// </summary>
public class QueryKnowledgeGraphSchemaCommand : IRequest<QueryKnowledgeGraphSchemaCommandResponse>
{
    /// <summary>
    /// 图谱 id.
    /// </summary>
    public long KgId { get; init; }
}
```

`Queries/Responses/KnowledgeGraphEntityTypeItem.cs`：

```csharp
namespace MoAI.KnowledgeGraph.Queries.Responses;

/// <summary>
/// 实体类型项.
/// </summary>
public class KnowledgeGraphEntityTypeItem
{
    /// <summary>
    /// 类型 id.
    /// </summary>
    public long EntityTypeId { get; init; }

    /// <summary>
    /// 名称.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 颜色.
    /// </summary>
    public string Color { get; init; } = string.Empty;

    /// <summary>
    /// 描述.
    /// </summary>
    public string Description { get; init; } = string.Empty;
}
```

`Queries/Responses/KnowledgeGraphRelationTypeItem.cs`：

```csharp
namespace MoAI.KnowledgeGraph.Queries.Responses;

/// <summary>
/// 关系类型项.
/// </summary>
public class KnowledgeGraphRelationTypeItem
{
    /// <summary>
    /// 关系类型 id.
    /// </summary>
    public long RelationTypeId { get; init; }

    /// <summary>
    /// 名称.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 颜色.
    /// </summary>
    public string Color { get; init; } = string.Empty;

    /// <summary>
    /// 描述.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// 起点实体类型 id，null=任意.
    /// </summary>
    public long? SourceTypeId { get; init; }

    /// <summary>
    /// 终点实体类型 id，null=任意.
    /// </summary>
    public long? TargetTypeId { get; init; }
}
```

`Queries/Responses/QueryKnowledgeGraphSchemaCommandResponse.cs`：

```csharp
namespace MoAI.KnowledgeGraph.Queries.Responses;

/// <summary>
/// 图谱 schema 响应.
/// </summary>
public class QueryKnowledgeGraphSchemaCommandResponse
{
    /// <summary>
    /// 实体类型.
    /// </summary>
    public List<KnowledgeGraphEntityTypeItem> EntityTypes { get; init; } = new();

    /// <summary>
    /// 关系类型.
    /// </summary>
    public List<KnowledgeGraphRelationTypeItem> RelationTypes { get; init; } = new();
}
```

- [ ] **Step 2: Handler**

`Handlers/CreateKnowledgeGraphEntityTypeCommandHandler.cs`：

```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.KnowledgeGraph.Commands;
using MoAI.KnowledgeGraph.Services;
using MoAI.Settings.Services;

namespace MoAI.KnowledgeGraph.Handlers;

/// <summary>
/// <inheritdoc cref="CreateKnowledgeGraphEntityTypeCommand"/>
/// </summary>
public class CreateKnowledgeGraphEntityTypeCommandHandler : IRequestHandler<CreateKnowledgeGraphEntityTypeCommand, SimpleLong>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IKnowledgeGraphAuthorizer _authorizer;
    private readonly IKnowledgeGraphSettingsService _settingsService;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateKnowledgeGraphEntityTypeCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="authorizer">权限判定.</param>
    /// <param name="settingsService">知识图谱设置.</param>
    public CreateKnowledgeGraphEntityTypeCommandHandler(DatabaseContext databaseContext, IKnowledgeGraphAuthorizer authorizer, IKnowledgeGraphSettingsService settingsService)
    {
        _databaseContext = databaseContext;
        _authorizer = authorizer;
        _settingsService = settingsService;
    }

    /// <inheritdoc/>
    public async Task<SimpleLong> Handle(CreateKnowledgeGraphEntityTypeCommand request, CancellationToken cancellationToken)
    {
        await _authorizer.AuthorizeAsync(request.KgId, adminOnly: true, cancellationToken);
        var settings = await _settingsService.GetAsync(cancellationToken);
        if (!settings.Enabled)
        {
            throw new BusinessException("未开启知识图谱能力.") { StatusCode = 409 };
        }

        var nameExist = await _databaseContext.KnowledgeGraphEntityTypes
            .AnyAsync(x => x.KgId == request.KgId && x.Name == request.Name, cancellationToken);
        if (nameExist)
        {
            throw new BusinessException("实体类型名称已存在.") { StatusCode = 409 };
        }

        var maxSort = await _databaseContext.KnowledgeGraphEntityTypes
            .Where(x => x.KgId == request.KgId)
            .Select(x => (int?)x.Sort)
            .MaxAsync(cancellationToken) ?? -1;

        var entity = new KnowledgeGraphEntityTypeEntity
        {
            KgId = request.KgId,
            Name = request.Name,
            Color = request.Color ?? string.Empty,
            Description = request.Description ?? string.Empty,
            Sort = maxSort + 1,
        };
        _databaseContext.KnowledgeGraphEntityTypes.Add(entity);
        await _databaseContext.SaveChangesAsync(cancellationToken);
        return new SimpleLong { Value = entity.Id };
    }
}
```

`Handlers/UpdateKnowledgeGraphEntityTypeCommandHandler.cs`：

```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.KnowledgeGraph.Commands;
using MoAI.KnowledgeGraph.Services;
using MoAI.Settings.Services;

namespace MoAI.KnowledgeGraph.Handlers;

/// <summary>
/// <inheritdoc cref="UpdateKnowledgeGraphEntityTypeCommand"/>
/// </summary>
public class UpdateKnowledgeGraphEntityTypeCommandHandler : IRequestHandler<UpdateKnowledgeGraphEntityTypeCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IKnowledgeGraphAuthorizer _authorizer;
    private readonly IKnowledgeGraphSettingsService _settingsService;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateKnowledgeGraphEntityTypeCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="authorizer">权限判定.</param>
    /// <param name="settingsService">知识图谱设置.</param>
    public UpdateKnowledgeGraphEntityTypeCommandHandler(DatabaseContext databaseContext, IKnowledgeGraphAuthorizer authorizer, IKnowledgeGraphSettingsService settingsService)
    {
        _databaseContext = databaseContext;
        _authorizer = authorizer;
        _settingsService = settingsService;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UpdateKnowledgeGraphEntityTypeCommand request, CancellationToken cancellationToken)
    {
        await _authorizer.AuthorizeAsync(request.KgId, adminOnly: true, cancellationToken);
        var settings = await _settingsService.GetAsync(cancellationToken);
        if (!settings.Enabled)
        {
            throw new BusinessException("未开启知识图谱能力.") { StatusCode = 409 };
        }

        var entity = await _databaseContext.KnowledgeGraphEntityTypes
            .FirstOrDefaultAsync(x => x.Id == request.EntityTypeId && x.KgId == request.KgId, cancellationToken)
            ?? throw new BusinessException("实体类型不存在.") { StatusCode = 404 };

        var nameExist = await _databaseContext.KnowledgeGraphEntityTypes
            .AnyAsync(x => x.KgId == request.KgId && x.Name == request.Name && x.Id != entity.Id, cancellationToken);
        if (nameExist)
        {
            throw new BusinessException("实体类型名称已存在.") { StatusCode = 409 };
        }

        entity.Name = request.Name;
        entity.Color = request.Color ?? string.Empty;
        entity.Description = request.Description ?? string.Empty;
        await _databaseContext.SaveChangesAsync(cancellationToken);
        return EmptyCommandResponse.Default;
    }
}
```

`Handlers/DeleteKnowledgeGraphEntityTypeCommandHandler.cs`：

```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.KnowledgeGraph.Commands;
using MoAI.KnowledgeGraph.Services;
using MoAI.Settings.Services;

namespace MoAI.KnowledgeGraph.Handlers;

/// <summary>
/// <inheritdoc cref="DeleteKnowledgeGraphEntityTypeCommand"/>
/// </summary>
public class DeleteKnowledgeGraphEntityTypeCommandHandler : IRequestHandler<DeleteKnowledgeGraphEntityTypeCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IKnowledgeGraphAuthorizer _authorizer;
    private readonly IKnowledgeGraphSettingsService _settingsService;
    private readonly IKnowledgeGraphStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteKnowledgeGraphEntityTypeCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="authorizer">权限判定.</param>
    /// <param name="settingsService">知识图谱设置.</param>
    /// <param name="store">图存储.</param>
    public DeleteKnowledgeGraphEntityTypeCommandHandler(DatabaseContext databaseContext, IKnowledgeGraphAuthorizer authorizer, IKnowledgeGraphSettingsService settingsService, IKnowledgeGraphStore store)
    {
        _databaseContext = databaseContext;
        _authorizer = authorizer;
        _settingsService = settingsService;
        _store = store;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(DeleteKnowledgeGraphEntityTypeCommand request, CancellationToken cancellationToken)
    {
        await _authorizer.AuthorizeAsync(request.KgId, adminOnly: true, cancellationToken);
        var settings = await _settingsService.GetAsync(cancellationToken);
        if (!settings.Enabled)
        {
            throw new BusinessException("未开启知识图谱能力.") { StatusCode = 409 };
        }

        var entity = await _databaseContext.KnowledgeGraphEntityTypes
            .FirstOrDefaultAsync(x => x.Id == request.EntityTypeId && x.KgId == request.KgId, cancellationToken)
            ?? throw new BusinessException("实体类型不存在.") { StatusCode = 404 };

        var nodeCount = await _store.CountNodesByEntityTypeAsync(request.KgId, request.EntityTypeId, cancellationToken);
        if (nodeCount > 0)
        {
            throw new BusinessException("该实体类型下仍有节点，无法删除.") { StatusCode = 409 };
        }

        _databaseContext.KnowledgeGraphEntityTypes.Remove(entity);
        await _databaseContext.SaveChangesAsync(cancellationToken);
        return EmptyCommandResponse.Default;
    }
}
```

`Handlers/QueryKnowledgeGraphSchemaCommandHandler.cs`：

```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.KnowledgeGraph.Queries;
using MoAI.KnowledgeGraph.Queries.Responses;
using MoAI.KnowledgeGraph.Services;

namespace MoAI.KnowledgeGraph.Handlers;

/// <summary>
/// <inheritdoc cref="QueryKnowledgeGraphSchemaCommand"/>
/// </summary>
public class QueryKnowledgeGraphSchemaCommandHandler : IRequestHandler<QueryKnowledgeGraphSchemaCommand, QueryKnowledgeGraphSchemaCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IKnowledgeGraphAuthorizer _authorizer;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryKnowledgeGraphSchemaCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="authorizer">权限判定.</param>
    public QueryKnowledgeGraphSchemaCommandHandler(DatabaseContext databaseContext, IKnowledgeGraphAuthorizer authorizer)
    {
        _databaseContext = databaseContext;
        _authorizer = authorizer;
    }

    /// <inheritdoc/>
    public async Task<QueryKnowledgeGraphSchemaCommandResponse> Handle(QueryKnowledgeGraphSchemaCommand request, CancellationToken cancellationToken)
    {
        await _authorizer.AuthorizeAsync(request.KgId, adminOnly: false, cancellationToken);

        var entityTypes = await _databaseContext.KnowledgeGraphEntityTypes
            .Where(x => x.KgId == request.KgId)
            .OrderBy(x => x.Sort).ThenBy(x => x.Id)
            .Select(x => new KnowledgeGraphEntityTypeItem
            {
                EntityTypeId = x.Id,
                Name = x.Name,
                Color = x.Color,
                Description = x.Description,
            })
            .ToListAsync(cancellationToken);

        var relationTypes = await _databaseContext.KnowledgeGraphRelationTypes
            .Where(x => x.KgId == request.KgId)
            .OrderBy(x => x.Sort).ThenBy(x => x.Id)
            .Select(x => new KnowledgeGraphRelationTypeItem
            {
                RelationTypeId = x.Id,
                Name = x.Name,
                Color = x.Color,
                Description = x.Description,
                SourceTypeId = x.SourceTypeId,
                TargetTypeId = x.TargetTypeId,
            })
            .ToListAsync(cancellationToken);

        return new QueryKnowledgeGraphSchemaCommandResponse { EntityTypes = entityTypes, RelationTypes = relationTypes };
    }
}
```

关系类型三个 Handler 与实体类型三个 Handler 结构一致，差异仅在实体名（`KnowledgeGraphRelationTypeEntity`）与字段（`SourceTypeId`/`TargetTypeId`、`RelationTypeId`）以及删除前调用 `_store.CountEdgesByRelationTypeAsync(request.KgId, request.RelationTypeId, ct)`。按上面实体类型 Handler 逐一改写：

- `CreateKnowledgeGraphRelationTypeCommandHandler`：校验 `SourceTypeId`/`TargetTypeId` 若非空则查 `KnowledgeGraphEntityTypes` 是否存在（不存在 → 400「关联的实体类型不存在」），然后落库并返回 `SimpleLong`
- `UpdateKnowledgeGraphRelationTypeCommandHandler`：同校验 + 名称唯一
- `DeleteKnowledgeGraphRelationTypeCommandHandler`：先 `CountEdgesByRelationTypeAsync` >0 → 409「该关系类型下仍有关系，无法删除」

- [ ] **Step 3: 构建**

Run: `dotnet build src/MoAI/MoAI.csproj`
Expected: 0 error

- [ ] **Step 4: 提交**

```bash
git add src/knowledgegraph
git commit -m "feat(knowledge-graph): schema（实体类型/关系类型）CRUD"
```

---

## Task 7: 节点与边 CRUD

**Files:**
- Create: `.../Shared/Commands/CreateKnowledgeGraphNodeCommand.cs`、`UpdateKnowledgeGraphNodeCommand.cs`、`DeleteKnowledgeGraphNodeCommand.cs`
- Create: `.../Shared/Commands/CreateKnowledgeGraphEdgeCommand.cs`、`UpdateKnowledgeGraphEdgeCommand.cs`、`DeleteKnowledgeGraphEdgeCommand.cs`
- Create: `.../Shared/Queries/QueryKnowledgeGraphNodesCommand.cs`、`QueryKnowledgeGraphNodeCommand.cs`、`QueryKnowledgeGraphEdgesCommand.cs`、`QueryKnowledgeGraphEdgeCommand.cs`
- Create: `.../Shared/Queries/Responses/KnowledgeGraphNodeItem.cs`、`QueryKnowledgeGraphNodesCommandResponse.cs`、`QueryKnowledgeGraphNodeCommandResponse.cs`、`KnowledgeGraphEdgeItem.cs`、`QueryKnowledgeGraphEdgesCommandResponse.cs`、`QueryKnowledgeGraphEdgeCommandResponse.cs`
- Create: `.../Core/Handlers/*Node*.cs`、`*Edge*.cs`

**命令字段（全部 `init`，实现 `IModelValidator<T>`，`KgId>0`）**
- Node 增：`KgId, EntityTypeId, Name, Description?`；改/删：`KgId, NodeId(string), EntityTypeId, Name, Description?`
- Edge 增：`KgId, RelationTypeId, SourceNodeId(string), TargetNodeId(string)`；改：`KgId, EdgeId, RelationTypeId`；删：`KgId, EdgeId`
- 列表查询（POST body）：`KgId, EntityTypeId?, Keyword?, PageNo=1, PageSize=20` / `KgId, RelationTypeId?, NodeId?, PageNo=1, PageSize=20`
- 详情查询：`KgId, NodeId` / `KgId, EdgeId`

**Handler 规则**
- 权限：读写均 `AuthorizeAsync(KgId, adminOnly:false)`
- 建节点：`EntityTypeId` 必须属于该图谱（查 `KnowledgeGraphEntityTypes`），否则 400
- 建边：`RelationTypeId` 属于该图谱；`SourceNodeId`/`TargetNodeId` 用 `_store.GetNodeAsync` 校验存在；关系类型若设置 `SourceTypeId`/`TargetTypeId`，端点节点 `EntityTypeId` 必须匹配，否则 400
- 改节点：先 `GetNodeAsync` 不存在 → 404；校验实体类型归属
- 删节点：`_store.DeleteNodeAsync`，false → 404
- 改/删边：false → 404
- 列表：`PageSize` clamp 到 `1..100`，`PageNo>=1`

- [ ] **Step 1: 命令（节点）**

`Commands/CreateKnowledgeGraphNodeCommand.cs`：

```csharp
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.KnowledgeGraph.Commands;

/// <summary>
/// 新增节点.
/// </summary>
public class CreateKnowledgeGraphNodeCommand : IRequest<SimpleString>, IModelValidator<CreateKnowledgeGraphNodeCommand>
{
    /// <summary>
    /// 图谱 id.
    /// </summary>
    public long KgId { get; init; }

    /// <summary>
    /// 实体类型 id.
    /// </summary>
    public long EntityTypeId { get; init; }

    /// <summary>
    /// 名称.
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// 描述.
    /// </summary>
    public string? Description { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<CreateKnowledgeGraphNodeCommand> validate)
    {
        validate.RuleFor(x => x.KgId).GreaterThan(0).WithMessage("图谱 id 不正确.");
        validate.RuleFor(x => x.EntityTypeId).GreaterThan(0).WithMessage("实体类型 id 不正确.");
        validate.RuleFor(x => x.Name).NotEmpty().WithMessage("节点名称不能为空.").MaximumLength(200).WithMessage("节点名称最长 200 个字符.");
        validate.RuleFor(x => x.Description).MaximumLength(1000).WithMessage("描述最长 1000 个字符.");
    }
}
```

`Commands/UpdateKnowledgeGraphNodeCommand.cs`：

```csharp
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.KnowledgeGraph.Commands;

/// <summary>
/// 修改节点.
/// </summary>
public class UpdateKnowledgeGraphNodeCommand : IRequest<EmptyCommandResponse>, IModelValidator<UpdateKnowledgeGraphNodeCommand>
{
    /// <summary>
    /// 图谱 id.
    /// </summary>
    public long KgId { get; init; }

    /// <summary>
    /// 节点 id.
    /// </summary>
    public string NodeId { get; init; } = default!;

    /// <summary>
    /// 实体类型 id.
    /// </summary>
    public long EntityTypeId { get; init; }

    /// <summary>
    /// 名称.
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// 描述.
    /// </summary>
    public string? Description { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<UpdateKnowledgeGraphNodeCommand> validate)
    {
        validate.RuleFor(x => x.KgId).GreaterThan(0).WithMessage("图谱 id 不正确.");
        validate.RuleFor(x => x.NodeId).NotEmpty().WithMessage("节点 id 不正确.");
        validate.RuleFor(x => x.EntityTypeId).GreaterThan(0).WithMessage("实体类型 id 不正确.");
        validate.RuleFor(x => x.Name).NotEmpty().WithMessage("节点名称不能为空.").MaximumLength(200).WithMessage("节点名称最长 200 个字符.");
        validate.RuleFor(x => x.Description).MaximumLength(1000).WithMessage("描述最长 1000 个字符.");
    }
}
```

`Commands/DeleteKnowledgeGraphNodeCommand.cs`：

```csharp
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.KnowledgeGraph.Commands;

/// <summary>
/// 删除节点（连带其边）.
/// </summary>
public class DeleteKnowledgeGraphNodeCommand : IRequest<EmptyCommandResponse>, IModelValidator<DeleteKnowledgeGraphNodeCommand>
{
    /// <summary>
    /// 图谱 id.
    /// </summary>
    public long KgId { get; init; }

    /// <summary>
    /// 节点 id.
    /// </summary>
    public string NodeId { get; init; } = default!;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<DeleteKnowledgeGraphNodeCommand> validate)
    {
        validate.RuleFor(x => x.KgId).GreaterThan(0).WithMessage("图谱 id 不正确.");
        validate.RuleFor(x => x.NodeId).NotEmpty().WithMessage("节点 id 不正确.");
    }
}
```

- [ ] **Step 2: 命令（边）**

`Commands/CreateKnowledgeGraphEdgeCommand.cs`：

```csharp
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.KnowledgeGraph.Commands;

/// <summary>
/// 新增边.
/// </summary>
public class CreateKnowledgeGraphEdgeCommand : IRequest<SimpleString>, IModelValidator<CreateKnowledgeGraphEdgeCommand>
{
    /// <summary>
    /// 图谱 id.
    /// </summary>
    public long KgId { get; init; }

    /// <summary>
    /// 关系类型 id.
    /// </summary>
    public long RelationTypeId { get; init; }

    /// <summary>
    /// 起点节点 id.
    /// </summary>
    public string SourceNodeId { get; init; } = default!;

    /// <summary>
    /// 终点节点 id.
    /// </summary>
    public string TargetNodeId { get; init; } = default!;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<CreateKnowledgeGraphEdgeCommand> validate)
    {
        validate.RuleFor(x => x.KgId).GreaterThan(0).WithMessage("图谱 id 不正确.");
        validate.RuleFor(x => x.RelationTypeId).GreaterThan(0).WithMessage("关系类型 id 不正确.");
        validate.RuleFor(x => x.SourceNodeId).NotEmpty().WithMessage("起点节点不正确.");
        validate.RuleFor(x => x.TargetNodeId).NotEmpty().WithMessage("终点节点不正确.");
    }
}
```

`Commands/UpdateKnowledgeGraphEdgeCommand.cs`：

```csharp
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.KnowledgeGraph.Commands;

/// <summary>
/// 修改边.
/// </summary>
public class UpdateKnowledgeGraphEdgeCommand : IRequest<EmptyCommandResponse>, IModelValidator<UpdateKnowledgeGraphEdgeCommand>
{
    /// <summary>
    /// 图谱 id.
    /// </summary>
    public long KgId { get; init; }

    /// <summary>
    /// 边 id.
    /// </summary>
    public string EdgeId { get; init; } = default!;

    /// <summary>
    /// 关系类型 id.
    /// </summary>
    public long RelationTypeId { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<UpdateKnowledgeGraphEdgeCommand> validate)
    {
        validate.RuleFor(x => x.KgId).GreaterThan(0).WithMessage("图谱 id 不正确.");
        validate.RuleFor(x => x.EdgeId).NotEmpty().WithMessage("边 id 不正确.");
        validate.RuleFor(x => x.RelationTypeId).GreaterThan(0).WithMessage("关系类型 id 不正确.");
    }
}
```

`Commands/DeleteKnowledgeGraphEdgeCommand.cs`：

```csharp
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.KnowledgeGraph.Commands;

/// <summary>
/// 删除边.
/// </summary>
public class DeleteKnowledgeGraphEdgeCommand : IRequest<EmptyCommandResponse>, IModelValidator<DeleteKnowledgeGraphEdgeCommand>
{
    /// <summary>
    /// 图谱 id.
    /// </summary>
    public long KgId { get; init; }

    /// <summary>
    /// 边 id.
    /// </summary>
    public string EdgeId { get; init; } = default!;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<DeleteKnowledgeGraphEdgeCommand> validate)
    {
        validate.RuleFor(x => x.KgId).GreaterThan(0).WithMessage("图谱 id 不正确.");
        validate.RuleFor(x => x.EdgeId).NotEmpty().WithMessage("边 id 不正确.");
    }
}
```

- [ ] **Step 3: 查询与响应**

`Queries/QueryKnowledgeGraphNodesCommand.cs`：

```csharp
using MediatR;
using MoAI.KnowledgeGraph.Queries.Responses;

namespace MoAI.KnowledgeGraph.Queries;

/// <summary>
/// 分页查询节点.
/// </summary>
public class QueryKnowledgeGraphNodesCommand : IRequest<QueryKnowledgeGraphNodesCommandResponse>
{
    /// <summary>
    /// 图谱 id.
    /// </summary>
    public long KgId { get; init; }

    /// <summary>
    /// 实体类型筛选.
    /// </summary>
    public long? EntityTypeId { get; init; }

    /// <summary>
    /// 名称关键字.
    /// </summary>
    public string? Keyword { get; init; }

    /// <summary>
    /// 页码（从 1 开始）.
    /// </summary>
    public int PageNo { get; init; } = 1;

    /// <summary>
    /// 每页数量.
    /// </summary>
    public int PageSize { get; init; } = 20;
}
```

`Queries/QueryKnowledgeGraphNodeCommand.cs`：

```csharp
using MediatR;
using MoAI.KnowledgeGraph.Queries.Responses;

namespace MoAI.KnowledgeGraph.Queries;

/// <summary>
/// 查询节点详情.
/// </summary>
public class QueryKnowledgeGraphNodeCommand : IRequest<QueryKnowledgeGraphNodeCommandResponse>
{
    /// <summary>
    /// 图谱 id.
    /// </summary>
    public long KgId { get; init; }

    /// <summary>
    /// 节点 id.
    /// </summary>
    public string NodeId { get; init; } = default!;
}
```

`Queries/QueryKnowledgeGraphEdgesCommand.cs`：

```csharp
using MediatR;
using MoAI.KnowledgeGraph.Queries.Responses;

namespace MoAI.KnowledgeGraph.Queries;

/// <summary>
/// 分页查询边.
/// </summary>
public class QueryKnowledgeGraphEdgesCommand : IRequest<QueryKnowledgeGraphEdgesCommandResponse>
{
    /// <summary>
    /// 图谱 id.
    /// </summary>
    public long KgId { get; init; }

    /// <summary>
    /// 关系类型筛选.
    /// </summary>
    public long? RelationTypeId { get; init; }

    /// <summary>
    /// 端点节点筛选.
    /// </summary>
    public string? NodeId { get; init; }

    /// <summary>
    /// 页码（从 1 开始）.
    /// </summary>
    public int PageNo { get; init; } = 1;

    /// <summary>
    /// 每页数量.
    /// </summary>
    public int PageSize { get; init; } = 20;
}
```

`Queries/QueryKnowledgeGraphEdgeCommand.cs`：

```csharp
using MediatR;
using MoAI.KnowledgeGraph.Queries.Responses;

namespace MoAI.KnowledgeGraph.Queries;

/// <summary>
/// 查询边详情.
/// </summary>
public class QueryKnowledgeGraphEdgeCommand : IRequest<QueryKnowledgeGraphEdgeCommandResponse>
{
    /// <summary>
    /// 图谱 id.
    /// </summary>
    public long KgId { get; init; }

    /// <summary>
    /// 边 id.
    /// </summary>
    public string EdgeId { get; init; } = default!;
}
```

响应：

```csharp
// KnowledgeGraphNodeItem.cs
namespace MoAI.KnowledgeGraph.Queries.Responses;

/// <summary>
/// 节点列表项.
/// </summary>
public class KnowledgeGraphNodeItem
{
    /// <summary>
    /// 节点 id.
    /// </summary>
    public string NodeId { get; init; } = string.Empty;

    /// <summary>
    /// 实体类型 id.
    /// </summary>
    public long EntityTypeId { get; init; }

    /// <summary>
    /// 名称.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 描述.
    /// </summary>
    public string Description { get; init; } = string.Empty;
}
```

```csharp
// QueryKnowledgeGraphNodesCommandResponse.cs
namespace MoAI.KnowledgeGraph.Queries.Responses;

/// <summary>
/// 节点分页响应.
/// </summary>
public class QueryKnowledgeGraphNodesCommandResponse
{
    /// <summary>
    /// 列表.
    /// </summary>
    public List<KnowledgeGraphNodeItem> Items { get; init; } = new();

    /// <summary>
    /// 总数.
    /// </summary>
    public long Total { get; init; }
}
```

```csharp
// QueryKnowledgeGraphNodeCommandResponse.cs
namespace MoAI.KnowledgeGraph.Queries.Responses;

/// <summary>
/// 节点详情响应.
/// </summary>
public class QueryKnowledgeGraphNodeCommandResponse
{
    /// <summary>
    /// 节点 id.
    /// </summary>
    public string NodeId { get; init; } = string.Empty;

    /// <summary>
    /// 实体类型 id.
    /// </summary>
    public long EntityTypeId { get; init; }

    /// <summary>
    /// 名称.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 描述.
    /// </summary>
    public string Description { get; init; } = string.Empty;
}
```

```csharp
// KnowledgeGraphEdgeItem.cs
namespace MoAI.KnowledgeGraph.Queries.Responses;

/// <summary>
/// 边列表项.
/// </summary>
public class KnowledgeGraphEdgeItem
{
    /// <summary>
    /// 边 id.
    /// </summary>
    public string EdgeId { get; init; } = string.Empty;

    /// <summary>
    /// 关系类型 id.
    /// </summary>
    public long RelationTypeId { get; init; }

    /// <summary>
    /// 起点节点 id.
    /// </summary>
    public string SourceNodeId { get; init; } = string.Empty;

    /// <summary>
    /// 终点节点 id.
    /// </summary>
    public string TargetNodeId { get; init; } = string.Empty;
}
```

```csharp
// QueryKnowledgeGraphEdgesCommandResponse.cs
namespace MoAI.KnowledgeGraph.Queries.Responses;

/// <summary>
/// 边分页响应.
/// </summary>
public class QueryKnowledgeGraphEdgesCommandResponse
{
    /// <summary>
    /// 列表.
    /// </summary>
    public List<KnowledgeGraphEdgeItem> Items { get; init; } = new();

    /// <summary>
    /// 总数.
    /// </summary>
    public long Total { get; init; }
}
```

```csharp
// QueryKnowledgeGraphEdgeCommandResponse.cs
namespace MoAI.KnowledgeGraph.Queries.Responses;

/// <summary>
/// 边详情响应.
/// </summary>
public class QueryKnowledgeGraphEdgeCommandResponse
{
    /// <summary>
    /// 边 id.
    /// </summary>
    public string EdgeId { get; init; } = string.Empty;

    /// <summary>
    /// 关系类型 id.
    /// </summary>
    public long RelationTypeId { get; init; }

    /// <summary>
    /// 起点节点 id.
    /// </summary>
    public string SourceNodeId { get; init; } = string.Empty;

    /// <summary>
    /// 终点节点 id.
    /// </summary>
    public string TargetNodeId { get; init; } = string.Empty;
}
```

- [ ] **Step 4: Handler（节点与边）**

`Handlers/CreateKnowledgeGraphNodeCommandHandler.cs`：

```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.KnowledgeGraph.Commands;
using MoAI.KnowledgeGraph.Services;

namespace MoAI.KnowledgeGraph.Handlers;

/// <summary>
/// <inheritdoc cref="CreateKnowledgeGraphNodeCommand"/>
/// </summary>
public class CreateKnowledgeGraphNodeCommandHandler : IRequestHandler<CreateKnowledgeGraphNodeCommand, SimpleString>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IKnowledgeGraphAuthorizer _authorizer;
    private readonly IKnowledgeGraphStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateKnowledgeGraphNodeCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="authorizer">权限判定.</param>
    /// <param name="store">图存储.</param>
    public CreateKnowledgeGraphNodeCommandHandler(DatabaseContext databaseContext, IKnowledgeGraphAuthorizer authorizer, IKnowledgeGraphStore store)
    {
        _databaseContext = databaseContext;
        _authorizer = authorizer;
        _store = store;
    }

    /// <inheritdoc/>
    public async Task<SimpleString> Handle(CreateKnowledgeGraphNodeCommand request, CancellationToken cancellationToken)
    {
        await _authorizer.AuthorizeAsync(request.KgId, adminOnly: false, cancellationToken);

        var typeExists = await _databaseContext.KnowledgeGraphEntityTypes
            .AnyAsync(x => x.Id == request.EntityTypeId && x.KgId == request.KgId, cancellationToken);
        if (!typeExists)
        {
            throw new BusinessException("实体类型不存在.") { StatusCode = 400 };
        }

        var node = await _store.CreateNodeAsync(request.KgId, request.EntityTypeId, request.Name, request.Description ?? string.Empty, cancellationToken);
        return new SimpleString { Value = node.Id };
    }
}
```

`Handlers/UpdateKnowledgeGraphNodeCommandHandler.cs`：

```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.KnowledgeGraph.Commands;
using MoAI.KnowledgeGraph.Services;

namespace MoAI.KnowledgeGraph.Handlers;

/// <summary>
/// <inheritdoc cref="UpdateKnowledgeGraphNodeCommand"/>
/// </summary>
public class UpdateKnowledgeGraphNodeCommandHandler : IRequestHandler<UpdateKnowledgeGraphNodeCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IKnowledgeGraphAuthorizer _authorizer;
    private readonly IKnowledgeGraphStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateKnowledgeGraphNodeCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="authorizer">权限判定.</param>
    /// <param name="store">图存储.</param>
    public UpdateKnowledgeGraphNodeCommandHandler(DatabaseContext databaseContext, IKnowledgeGraphAuthorizer authorizer, IKnowledgeGraphStore store)
    {
        _databaseContext = databaseContext;
        _authorizer = authorizer;
        _store = store;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UpdateKnowledgeGraphNodeCommand request, CancellationToken cancellationToken)
    {
        await _authorizer.AuthorizeAsync(request.KgId, adminOnly: false, cancellationToken);

        _ = await _store.GetNodeAsync(request.KgId, request.NodeId, cancellationToken)
            ?? throw new BusinessException("节点不存在.") { StatusCode = 404 };

        var typeExists = await _databaseContext.KnowledgeGraphEntityTypes
            .AnyAsync(x => x.Id == request.EntityTypeId && x.KgId == request.KgId, cancellationToken);
        if (!typeExists)
        {
            throw new BusinessException("实体类型不存在.") { StatusCode = 400 };
        }

        await _store.UpdateNodeAsync(request.KgId, request.NodeId, request.EntityTypeId, request.Name, request.Description ?? string.Empty, cancellationToken);
        return EmptyCommandResponse.Default;
    }
}
```

`Handlers/DeleteKnowledgeGraphNodeCommandHandler.cs`：

```csharp
using MediatR;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.KnowledgeGraph.Commands;
using MoAI.KnowledgeGraph.Services;

namespace MoAI.KnowledgeGraph.Handlers;

/// <summary>
/// <inheritdoc cref="DeleteKnowledgeGraphNodeCommand"/>
/// </summary>
public class DeleteKnowledgeGraphNodeCommandHandler : IRequestHandler<DeleteKnowledgeGraphNodeCommand, EmptyCommandResponse>
{
    private readonly IKnowledgeGraphAuthorizer _authorizer;
    private readonly IKnowledgeGraphStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteKnowledgeGraphNodeCommandHandler"/> class.
    /// </summary>
    /// <param name="authorizer">权限判定.</param>
    /// <param name="store">图存储.</param>
    public DeleteKnowledgeGraphNodeCommandHandler(IKnowledgeGraphAuthorizer authorizer, IKnowledgeGraphStore store)
    {
        _authorizer = authorizer;
        _store = store;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(DeleteKnowledgeGraphNodeCommand request, CancellationToken cancellationToken)
    {
        await _authorizer.AuthorizeAsync(request.KgId, adminOnly: false, cancellationToken);
        var deleted = await _store.DeleteNodeAsync(request.KgId, request.NodeId, cancellationToken);
        if (!deleted)
        {
            throw new BusinessException("节点不存在.") { StatusCode = 404 };
        }

        return EmptyCommandResponse.Default;
    }
}
```

`Handlers/QueryKnowledgeGraphNodesCommandHandler.cs`：

```csharp
using MediatR;
using MoAI.KnowledgeGraph.Queries;
using MoAI.KnowledgeGraph.Queries.Responses;
using MoAI.KnowledgeGraph.Services;

namespace MoAI.KnowledgeGraph.Handlers;

/// <summary>
/// <inheritdoc cref="QueryKnowledgeGraphNodesCommand"/>
/// </summary>
public class QueryKnowledgeGraphNodesCommandHandler : IRequestHandler<QueryKnowledgeGraphNodesCommand, QueryKnowledgeGraphNodesCommandResponse>
{
    private readonly IKnowledgeGraphAuthorizer _authorizer;
    private readonly IKnowledgeGraphStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryKnowledgeGraphNodesCommandHandler"/> class.
    /// </summary>
    /// <param name="authorizer">权限判定.</param>
    /// <param name="store">图存储.</param>
    public QueryKnowledgeGraphNodesCommandHandler(IKnowledgeGraphAuthorizer authorizer, IKnowledgeGraphStore store)
    {
        _authorizer = authorizer;
        _store = store;
    }

    /// <inheritdoc/>
    public async Task<QueryKnowledgeGraphNodesCommandResponse> Handle(QueryKnowledgeGraphNodesCommand request, CancellationToken cancellationToken)
    {
        await _authorizer.AuthorizeAsync(request.KgId, adminOnly: false, cancellationToken);
        var pageNo = request.PageNo < 1 ? 1 : request.PageNo;
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var (items, total) = await _store.ListNodesAsync(request.KgId, request.EntityTypeId, request.Keyword, pageNo, pageSize, cancellationToken);
        return new QueryKnowledgeGraphNodesCommandResponse
        {
            Total = total,
            Items = items.Select(x => new KnowledgeGraphNodeItem
            {
                NodeId = x.Id,
                EntityTypeId = x.EntityTypeId,
                Name = x.Name,
                Description = x.Description,
            }).ToList(),
        };
    }
}
```

`Handlers/QueryKnowledgeGraphNodeCommandHandler.cs`：

```csharp
using MediatR;
using MoAI.Infra.Exceptions;
using MoAI.KnowledgeGraph.Queries;
using MoAI.KnowledgeGraph.Queries.Responses;
using MoAI.KnowledgeGraph.Services;

namespace MoAI.KnowledgeGraph.Handlers;

/// <summary>
/// <inheritdoc cref="QueryKnowledgeGraphNodeCommand"/>
/// </summary>
public class QueryKnowledgeGraphNodeCommandHandler : IRequestHandler<QueryKnowledgeGraphNodeCommand, QueryKnowledgeGraphNodeCommandResponse>
{
    private readonly IKnowledgeGraphAuthorizer _authorizer;
    private readonly IKnowledgeGraphStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryKnowledgeGraphNodeCommandHandler"/> class.
    /// </summary>
    /// <param name="authorizer">权限判定.</param>
    /// <param name="store">图存储.</param>
    public QueryKnowledgeGraphNodeCommandHandler(IKnowledgeGraphAuthorizer authorizer, IKnowledgeGraphStore store)
    {
        _authorizer = authorizer;
        _store = store;
    }

    /// <inheritdoc/>
    public async Task<QueryKnowledgeGraphNodeCommandResponse> Handle(QueryKnowledgeGraphNodeCommand request, CancellationToken cancellationToken)
    {
        await _authorizer.AuthorizeAsync(request.KgId, adminOnly: false, cancellationToken);
        var node = await _store.GetNodeAsync(request.KgId, request.NodeId, cancellationToken)
            ?? throw new BusinessException("节点不存在.") { StatusCode = 404 };

        return new QueryKnowledgeGraphNodeCommandResponse
        {
            NodeId = node.Id,
            EntityTypeId = node.EntityTypeId,
            Name = node.Name,
            Description = node.Description,
        };
    }
}
```

`Handlers/CreateKnowledgeGraphEdgeCommandHandler.cs`：

```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.KnowledgeGraph.Commands;
using MoAI.KnowledgeGraph.Services;
using MoAI.Settings.Services;

namespace MoAI.KnowledgeGraph.Handlers;

/// <summary>
/// <inheritdoc cref="CreateKnowledgeGraphEdgeCommand"/>
/// </summary>
public class CreateKnowledgeGraphEdgeCommandHandler : IRequestHandler<CreateKnowledgeGraphEdgeCommand, SimpleString>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IKnowledgeGraphAuthorizer _authorizer;
    private readonly IKnowledgeGraphStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateKnowledgeGraphEdgeCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="authorizer">权限判定.</param>
    /// <param name="store">图存储.</param>
    public CreateKnowledgeGraphEdgeCommandHandler(DatabaseContext databaseContext, IKnowledgeGraphAuthorizer authorizer, IKnowledgeGraphStore store)
    {
        _databaseContext = databaseContext;
        _authorizer = authorizer;
        _store = store;
    }

    /// <inheritdoc/>
    public async Task<SimpleString> Handle(CreateKnowledgeGraphEdgeCommand request, CancellationToken cancellationToken)
    {
        await _authorizer.AuthorizeAsync(request.KgId, adminOnly: false, cancellationToken);

        var relationType = await _databaseContext.KnowledgeGraphRelationTypes
            .FirstOrDefaultAsync(x => x.Id == request.RelationTypeId && x.KgId == request.KgId, cancellationToken)
            ?? throw new BusinessException("关系类型不存在.") { StatusCode = 400 };

        var source = await _store.GetNodeAsync(request.KgId, request.SourceNodeId, cancellationToken)
            ?? throw new BusinessException("起点节点不存在.") { StatusCode = 400 };
        var target = await _store.GetNodeAsync(request.KgId, request.TargetNodeId, cancellationToken)
            ?? throw new BusinessException("终点节点不存在.") { StatusCode = 400 };

        if (relationType.SourceTypeId != null && relationType.SourceTypeId != source.EntityTypeId)
        {
            throw new BusinessException("起点节点类型不符合关系约束.") { StatusCode = 400 };
        }

        if (relationType.TargetTypeId != null && relationType.TargetTypeId != target.EntityTypeId)
        {
            throw new BusinessException("终点节点类型不符合关系约束.") { StatusCode = 400 };
        }

        var edge = await _store.CreateEdgeAsync(request.KgId, request.RelationTypeId, request.SourceNodeId, request.TargetNodeId, cancellationToken);
        return new SimpleString { Value = edge.Id };
    }
}
```

`Handlers/UpdateKnowledgeGraphEdgeCommandHandler.cs`：

```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.KnowledgeGraph.Commands;
using MoAI.KnowledgeGraph.Services;

namespace MoAI.KnowledgeGraph.Handlers;

/// <summary>
/// <inheritdoc cref="UpdateKnowledgeGraphEdgeCommand"/>
/// </summary>
public class UpdateKnowledgeGraphEdgeCommandHandler : IRequestHandler<UpdateKnowledgeGraphEdgeCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IKnowledgeGraphAuthorizer _authorizer;
    private readonly IKnowledgeGraphStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateKnowledgeGraphEdgeCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="authorizer">权限判定.</param>
    /// <param name="store">图存储.</param>
    public UpdateKnowledgeGraphEdgeCommandHandler(DatabaseContext databaseContext, IKnowledgeGraphAuthorizer authorizer, IKnowledgeGraphStore store)
    {
        _databaseContext = databaseContext;
        _authorizer = authorizer;
        _store = store;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UpdateKnowledgeGraphEdgeCommand request, CancellationToken cancellationToken)
    {
        await _authorizer.AuthorizeAsync(request.KgId, adminOnly: false, cancellationToken);

        var relationType = await _databaseContext.KnowledgeGraphRelationTypes
            .FirstOrDefaultAsync(x => x.Id == request.RelationTypeId && x.KgId == request.KgId, cancellationToken)
            ?? throw new BusinessException("关系类型不存在.") { StatusCode = 400 };

        var edge = await _store.GetEdgeAsync(request.KgId, request.EdgeId, cancellationToken)
            ?? throw new BusinessException("边不存在.") { StatusCode = 404 };

        if (relationType.SourceTypeId != null || relationType.TargetTypeId != null)
        {
            var source = await _store.GetNodeAsync(request.KgId, edge.SourceNodeId, cancellationToken);
            var target = await _store.GetNodeAsync(request.KgId, edge.TargetNodeId, cancellationToken);
            if (source == null || target == null)
            {
                throw new BusinessException("边端点节点不存在.") { StatusCode = 400 };
            }

            if (relationType.SourceTypeId != null && relationType.SourceTypeId != source.EntityTypeId)
            {
                throw new BusinessException("起点节点类型不符合关系约束.") { StatusCode = 400 };
            }

            if (relationType.TargetTypeId != null && relationType.TargetTypeId != target.EntityTypeId)
            {
                throw new BusinessException("终点节点类型不符合关系约束.") { StatusCode = 400 };
            }
        }

        var updated = await _store.UpdateEdgeAsync(request.KgId, request.EdgeId, request.RelationTypeId, cancellationToken);
        if (!updated)
        {
            throw new BusinessException("边不存在.") { StatusCode = 404 };
        }

        return EmptyCommandResponse.Default;
    }
}
```

`Handlers/DeleteKnowledgeGraphEdgeCommandHandler.cs`：

```csharp
using MediatR;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.KnowledgeGraph.Commands;
using MoAI.KnowledgeGraph.Services;

namespace MoAI.KnowledgeGraph.Handlers;

/// <summary>
/// <inheritdoc cref="DeleteKnowledgeGraphEdgeCommand"/>
/// </summary>
public class DeleteKnowledgeGraphEdgeCommandHandler : IRequestHandler<DeleteKnowledgeGraphEdgeCommand, EmptyCommandResponse>
{
    private readonly IKnowledgeGraphAuthorizer _authorizer;
    private readonly IKnowledgeGraphStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteKnowledgeGraphEdgeCommandHandler"/> class.
    /// </summary>
    /// <param name="authorizer">权限判定.</param>
    /// <param name="store">图存储.</param>
    public DeleteKnowledgeGraphEdgeCommandHandler(IKnowledgeGraphAuthorizer authorizer, IKnowledgeGraphStore store)
    {
        _authorizer = authorizer;
        _store = store;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(DeleteKnowledgeGraphEdgeCommand request, CancellationToken cancellationToken)
    {
        await _authorizer.AuthorizeAsync(request.KgId, adminOnly: false, cancellationToken);
        var deleted = await _store.DeleteEdgeAsync(request.KgId, request.EdgeId, cancellationToken);
        if (!deleted)
        {
            throw new BusinessException("边不存在.") { StatusCode = 404 };
        }

        return EmptyCommandResponse.Default;
    }
}
```

`Handlers/QueryKnowledgeGraphEdgesCommandHandler.cs`：

```csharp
using MediatR;
using MoAI.KnowledgeGraph.Queries;
using MoAI.KnowledgeGraph.Queries.Responses;
using MoAI.KnowledgeGraph.Services;

namespace MoAI.KnowledgeGraph.Handlers;

/// <summary>
/// <inheritdoc cref="QueryKnowledgeGraphEdgesCommand"/>
/// </summary>
public class QueryKnowledgeGraphEdgesCommandHandler : IRequestHandler<QueryKnowledgeGraphEdgesCommand, QueryKnowledgeGraphEdgesCommandResponse>
{
    private readonly IKnowledgeGraphAuthorizer _authorizer;
    private readonly IKnowledgeGraphStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryKnowledgeGraphEdgesCommandHandler"/> class.
    /// </summary>
    /// <param name="authorizer">权限判定.</param>
    /// <param name="store">图存储.</param>
    public QueryKnowledgeGraphEdgesCommandHandler(IKnowledgeGraphAuthorizer authorizer, IKnowledgeGraphStore store)
    {
        _authorizer = authorizer;
        _store = store;
    }

    /// <inheritdoc/>
    public async Task<QueryKnowledgeGraphEdgesCommandResponse> Handle(QueryKnowledgeGraphEdgesCommand request, CancellationToken cancellationToken)
    {
        await _authorizer.AuthorizeAsync(request.KgId, adminOnly: false, cancellationToken);
        var pageNo = request.PageNo < 1 ? 1 : request.PageNo;
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var (items, total) = await _store.ListEdgesAsync(request.KgId, request.RelationTypeId, request.NodeId, pageNo, pageSize, cancellationToken);
        return new QueryKnowledgeGraphEdgesCommandResponse
        {
            Total = total,
            Items = items.Select(x => new KnowledgeGraphEdgeItem
            {
                EdgeId = x.Id,
                RelationTypeId = x.RelationTypeId,
                SourceNodeId = x.SourceNodeId,
                TargetNodeId = x.TargetNodeId,
            }).ToList(),
        };
    }
}
```

`Handlers/QueryKnowledgeGraphEdgeCommandHandler.cs`：

```csharp
using MediatR;
using MoAI.Infra.Exceptions;
using MoAI.KnowledgeGraph.Queries;
using MoAI.KnowledgeGraph.Queries.Responses;
using MoAI.KnowledgeGraph.Services;

namespace MoAI.KnowledgeGraph.Handlers;

/// <summary>
/// <inheritdoc cref="QueryKnowledgeGraphEdgeCommand"/>
/// </summary>
public class QueryKnowledgeGraphEdgeCommandHandler : IRequestHandler<QueryKnowledgeGraphEdgeCommand, QueryKnowledgeGraphEdgeCommandResponse>
{
    private readonly IKnowledgeGraphAuthorizer _authorizer;
    private readonly IKnowledgeGraphStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryKnowledgeGraphEdgeCommandHandler"/> class.
    /// </summary>
    /// <param name="authorizer">权限判定.</param>
    /// <param name="store">图存储.</param>
    public QueryKnowledgeGraphEdgeCommandHandler(IKnowledgeGraphAuthorizer authorizer, IKnowledgeGraphStore store)
    {
        _authorizer = authorizer;
        _store = store;
    }

    /// <inheritdoc/>
    public async Task<QueryKnowledgeGraphEdgeCommandResponse> Handle(QueryKnowledgeGraphEdgeCommand request, CancellationToken cancellationToken)
    {
        await _authorizer.AuthorizeAsync(request.KgId, adminOnly: false, cancellationToken);
        var edge = await _store.GetEdgeAsync(request.KgId, request.EdgeId, cancellationToken)
            ?? throw new BusinessException("边不存在.") { StatusCode = 404 };

        return new QueryKnowledgeGraphEdgeCommandResponse
        {
            EdgeId = edge.Id,
            RelationTypeId = edge.RelationTypeId,
            SourceNodeId = edge.SourceNodeId,
            TargetNodeId = edge.TargetNodeId,
        };
    }
}
```

- [ ] **Step 5: 构建**

Run: `dotnet build src/MoAI/MoAI.csproj`
Expected: 0 error

- [ ] **Step 6: 提交**

```bash
git add src/knowledgegraph
git commit -m "feat(knowledge-graph): 节点与边 CRUD"
```

---

## Task 8: Controller（HTTP 接口）

**Files:**
- Create: `src/knowledgegraph/MoAI.KnowledgeGraph.Api/Controllers/KnowledgeGraphController.cs`

- [ ] **Step 1: 实现 Controller**

```csharp
using MediatR;
using Microsoft.AspNetCore.Mvc;
using MoAI.Infra.Models;
using MoAI.KnowledgeGraph.Commands;
using MoAI.KnowledgeGraph.Queries;
using MoAI.KnowledgeGraph.Queries.Responses;

namespace MoAI.KnowledgeGraph.Controllers;

/// <summary>
/// 知识图谱接口.
/// </summary>
[ApiController]
[Route("/knowledge-graph")]
public class KnowledgeGraphController : ControllerBase
{
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="KnowledgeGraphController"/> class.
    /// </summary>
    /// <param name="mediator">MediatR 实例.</param>
    public KnowledgeGraphController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// 创建知识图谱.
    /// </summary>
    /// <param name="req">创建请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>图谱 id.</returns>
    [HttpPost]
    public Task<SimpleLong> Create([FromBody] CreateKnowledgeGraphCommand req, CancellationToken ct)
        => _mediator.Send(req, ct);

    /// <summary>
    /// 查询团队下的知识图谱列表.
    /// </summary>
    /// <param name="teamId">团队 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>列表.</returns>
    [HttpGet("list")]
    public Task<QueryKnowledgeGraphsCommandResponse> List([FromQuery] long teamId, CancellationToken ct)
        => _mediator.Send(new QueryKnowledgeGraphsCommand { TeamId = teamId }, ct);

    /// <summary>
    /// 查询模板目录.
    /// </summary>
    /// <param name="ct">取消令牌.</param>
    /// <returns>模板列表.</returns>
    [HttpGet("templates")]
    public Task<QueryKnowledgeGraphTemplatesCommandResponse> Templates(CancellationToken ct)
        => _mediator.Send(new QueryKnowledgeGraphTemplatesCommand(), ct);

    /// <summary>
    /// 查询图谱详情.
    /// </summary>
    /// <param name="id">图谱 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>详情.</returns>
    [HttpGet("{id}")]
    public Task<QueryKnowledgeGraphCommandResponse> Detail(long id, CancellationToken ct)
        => _mediator.Send(new QueryKnowledgeGraphCommand { KgId = id }, ct);

    /// <summary>
    /// 更新图谱.
    /// </summary>
    /// <param name="id">图谱 id.</param>
    /// <param name="req">更新请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>空响应.</returns>
    [HttpPut("{id}")]
    public Task<EmptyCommandResponse> Update(long id, [FromBody] UpdateKnowledgeGraphCommand req, CancellationToken ct)
        => _mediator.Send(new UpdateKnowledgeGraphCommand { KgId = id, Name = req.Name, Description = req.Description }, ct);

    /// <summary>
    /// 删除图谱.
    /// </summary>
    /// <param name="id">图谱 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>空响应.</returns>
    [HttpDelete("{id}")]
    public Task<EmptyCommandResponse> Delete(long id, CancellationToken ct)
        => _mediator.Send(new DeleteKnowledgeGraphCommand { KgId = id }, ct);

    /// <summary>
    /// 查询 schema.
    /// </summary>
    /// <param name="id">图谱 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>schema.</returns>
    [HttpGet("{id}/schema")]
    public Task<QueryKnowledgeGraphSchemaCommandResponse> Schema(long id, CancellationToken ct)
        => _mediator.Send(new QueryKnowledgeGraphSchemaCommand { KgId = id }, ct);

    /// <summary>
    /// 新增实体类型.
    /// </summary>
    /// <param name="id">图谱 id.</param>
    /// <param name="req">请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>类型 id.</returns>
    [HttpPost("{id}/entity-types")]
    public Task<SimpleLong> CreateEntityType(long id, [FromBody] CreateKnowledgeGraphEntityTypeCommand req, CancellationToken ct)
        => _mediator.Send(new CreateKnowledgeGraphEntityTypeCommand { KgId = id, Name = req.Name, Color = req.Color, Description = req.Description }, ct);

    /// <summary>
    /// 修改实体类型.
    /// </summary>
    /// <param name="id">图谱 id.</param>
    /// <param name="typeId">实体类型 id.</param>
    /// <param name="req">请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>空响应.</returns>
    [HttpPut("{id}/entity-types/{typeId}")]
    public Task<EmptyCommandResponse> UpdateEntityType(long id, long typeId, [FromBody] UpdateKnowledgeGraphEntityTypeCommand req, CancellationToken ct)
        => _mediator.Send(new UpdateKnowledgeGraphEntityTypeCommand { KgId = id, EntityTypeId = typeId, Name = req.Name, Color = req.Color, Description = req.Description }, ct);

    /// <summary>
    /// 删除实体类型.
    /// </summary>
    /// <param name="id">图谱 id.</param>
    /// <param name="typeId">实体类型 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>空响应.</returns>
    [HttpDelete("{id}/entity-types/{typeId}")]
    public Task<EmptyCommandResponse> DeleteEntityType(long id, long typeId, CancellationToken ct)
        => _mediator.Send(new DeleteKnowledgeGraphEntityTypeCommand { KgId = id, EntityTypeId = typeId }, ct);

    /// <summary>
    /// 新增关系类型.
    /// </summary>
    /// <param name="id">图谱 id.</param>
    /// <param name="req">请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>类型 id.</returns>
    [HttpPost("{id}/relation-types")]
    public Task<SimpleLong> CreateRelationType(long id, [FromBody] CreateKnowledgeGraphRelationTypeCommand req, CancellationToken ct)
        => _mediator.Send(new CreateKnowledgeGraphRelationTypeCommand { KgId = id, Name = req.Name, Color = req.Color, Description = req.Description, SourceTypeId = req.SourceTypeId, TargetTypeId = req.TargetTypeId }, ct);

    /// <summary>
    /// 修改关系类型.
    /// </summary>
    /// <param name="id">图谱 id.</param>
    /// <param name="typeId">关系类型 id.</param>
    /// <param name="req">请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>空响应.</returns>
    [HttpPut("{id}/relation-types/{typeId}")]
    public Task<EmptyCommandResponse> UpdateRelationType(long id, long typeId, [FromBody] UpdateKnowledgeGraphRelationTypeCommand req, CancellationToken ct)
        => _mediator.Send(new UpdateKnowledgeGraphRelationTypeCommand { KgId = id, RelationTypeId = typeId, Name = req.Name, Color = req.Color, Description = req.Description, SourceTypeId = req.SourceTypeId, TargetTypeId = req.TargetTypeId }, ct);

    /// <summary>
    /// 删除关系类型.
    /// </summary>
    /// <param name="id">图谱 id.</param>
    /// <param name="typeId">关系类型 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>空响应.</returns>
    [HttpDelete("{id}/relation-types/{typeId}")]
    public Task<EmptyCommandResponse> DeleteRelationType(long id, long typeId, CancellationToken ct)
        => _mediator.Send(new DeleteKnowledgeGraphRelationTypeCommand { KgId = id, RelationTypeId = typeId }, ct);

    /// <summary>
    /// 节点分页.
    /// </summary>
    /// <param name="id">图谱 id.</param>
    /// <param name="req">查询请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>分页结果.</returns>
    [HttpPost("{id}/nodes/list")]
    public Task<QueryKnowledgeGraphNodesCommandResponse> ListNodes(long id, [FromBody] QueryKnowledgeGraphNodesCommand req, CancellationToken ct)
        => _mediator.Send(new QueryKnowledgeGraphNodesCommand { KgId = id, EntityTypeId = req.EntityTypeId, Keyword = req.Keyword, PageNo = req.PageNo, PageSize = req.PageSize }, ct);

    /// <summary>
    /// 新增节点.
    /// </summary>
    /// <param name="id">图谱 id.</param>
    /// <param name="req">请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>节点 id.</returns>
    [HttpPost("{id}/nodes")]
    public Task<SimpleString> CreateNode(long id, [FromBody] CreateKnowledgeGraphNodeCommand req, CancellationToken ct)
        => _mediator.Send(new CreateKnowledgeGraphNodeCommand { KgId = id, EntityTypeId = req.EntityTypeId, Name = req.Name, Description = req.Description }, ct);

    /// <summary>
    /// 节点详情.
    /// </summary>
    /// <param name="id">图谱 id.</param>
    /// <param name="nodeId">节点 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>节点.</returns>
    [HttpGet("{id}/nodes/{nodeId}")]
    public Task<QueryKnowledgeGraphNodeCommandResponse> NodeDetail(long id, string nodeId, CancellationToken ct)
        => _mediator.Send(new QueryKnowledgeGraphNodeCommand { KgId = id, NodeId = nodeId }, ct);

    /// <summary>
    /// 修改节点.
    /// </summary>
    /// <param name="id">图谱 id.</param>
    /// <param name="nodeId">节点 id.</param>
    /// <param name="req">请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>空响应.</returns>
    [HttpPut("{id}/nodes/{nodeId}")]
    public Task<EmptyCommandResponse> UpdateNode(long id, string nodeId, [FromBody] UpdateKnowledgeGraphNodeCommand req, CancellationToken ct)
        => _mediator.Send(new UpdateKnowledgeGraphNodeCommand { KgId = id, NodeId = nodeId, EntityTypeId = req.EntityTypeId, Name = req.Name, Description = req.Description }, ct);

    /// <summary>
    /// 删除节点.
    /// </summary>
    /// <param name="id">图谱 id.</param>
    /// <param name="nodeId">节点 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>空响应.</returns>
    [HttpDelete("{id}/nodes/{nodeId}")]
    public Task<EmptyCommandResponse> DeleteNode(long id, string nodeId, CancellationToken ct)
        => _mediator.Send(new DeleteKnowledgeGraphNodeCommand { KgId = id, NodeId = nodeId }, ct);

    /// <summary>
    /// 边分页.
    /// </summary>
    /// <param name="id">图谱 id.</param>
    /// <param name="req">查询请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>分页结果.</returns>
    [HttpPost("{id}/edges/list")]
    public Task<QueryKnowledgeGraphEdgesCommandResponse> ListEdges(long id, [FromBody] QueryKnowledgeGraphEdgesCommand req, CancellationToken ct)
        => _mediator.Send(new QueryKnowledgeGraphEdgesCommand { KgId = id, RelationTypeId = req.RelationTypeId, NodeId = req.NodeId, PageNo = req.PageNo, PageSize = req.PageSize }, ct);

    /// <summary>
    /// 新增边.
    /// </summary>
    /// <param name="id">图谱 id.</param>
    /// <param name="req">请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>边 id.</returns>
    [HttpPost("{id}/edges")]
    public Task<SimpleString> CreateEdge(long id, [FromBody] CreateKnowledgeGraphEdgeCommand req, CancellationToken ct)
        => _mediator.Send(new CreateKnowledgeGraphEdgeCommand { KgId = id, RelationTypeId = req.RelationTypeId, SourceNodeId = req.SourceNodeId, TargetNodeId = req.TargetNodeId }, ct);

    /// <summary>
    /// 边详情.
    /// </summary>
    /// <param name="id">图谱 id.</param>
    /// <param name="edgeId">边 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>边.</returns>
    [HttpGet("{id}/edges/{edgeId}")]
    public Task<QueryKnowledgeGraphEdgeCommandResponse> EdgeDetail(long id, string edgeId, CancellationToken ct)
        => _mediator.Send(new QueryKnowledgeGraphEdgeCommand { KgId = id, EdgeId = edgeId }, ct);

    /// <summary>
    /// 修改边.
    /// </summary>
    /// <param name="id">图谱 id.</param>
    /// <param name="edgeId">边 id.</param>
    /// <param name="req">请求.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>空响应.</returns>
    [HttpPut("{id}/edges/{edgeId}")]
    public Task<EmptyCommandResponse> UpdateEdge(long id, string edgeId, [FromBody] UpdateKnowledgeGraphEdgeCommand req, CancellationToken ct)
        => _mediator.Send(new UpdateKnowledgeGraphEdgeCommand { KgId = id, EdgeId = edgeId, RelationTypeId = req.RelationTypeId }, ct);

    /// <summary>
    /// 删除边.
    /// </summary>
    /// <param name="id">图谱 id.</param>
    /// <param name="edgeId">边 id.</param>
    /// <param name="ct">取消令牌.</param>
    /// <returns>空响应.</returns>
    [HttpDelete("{id}/edges/{edgeId}")]
    public Task<EmptyCommandResponse> DeleteEdge(long id, string edgeId, CancellationToken ct)
        => _mediator.Send(new DeleteKnowledgeGraphEdgeCommand { KgId = id, EdgeId = edgeId }, ct);
}
```

- [ ] **Step 2: 启动后端生成 OpenAPI**

Run: `dotnet run --project src/MoAI/MoAI.csproj`
Expected: 启动成功；浏览器/curl `http://127.0.0.1:5000/openapi/v1.json` 中出现 `/api/knowledge-graph/*`

- [ ] **Step 3: 提交**

```bash
git add src/knowledgegraph
git commit -m "feat(knowledge-graph): HTTP 接口"
```

---

## Task 9: E2E 脚本

**Files:**
- Create: `local-dev/kg-e2e.mjs`

- [ ] **Step 1: 编写脚本**

参照现有 `local-dev/wiki-e2e.mjs` 的登录/断言工具（复用其 `request`/`assert` 风格），覆盖 `KG-S1..S9`：

```
KG-S1  创建图谱（空白模板）
KG-S2  创建图谱（模板 ops，schema 应含 服务/人员/项目 + 维护/依赖）
KG-S3  同团队重名 409
KG-S4  新增实体类型、关系类型（带起止类型约束）
KG-S5  新增节点（类型不符实体类型不存在 400）
KG-S6  新增边（起止类型不符合约束 400）
KG-S7  边分页 / 节点分页
KG-S8  删除仍有节点的实体类型 409
KG-S9  删除图谱后节点/边清空（再查列表为空）
```

脚本必须：
- 先用 `OPEN_NEO4J` 检查能力开关；未开启时 `console.warn` 跳过并退出码 0
- 使用 root 账号 `admin/abcd123456` 登录，取团队 id（可复用 `getMyTeams` 对应 REST）
- 失败即 `process.exit(1)`，成功打印每场景 PASS

> 完整实现按现有 `local-dev/team-e2e.mjs` 的 HTTP 封装改写；不要引入新依赖。

- [ ] **Step 2: 运行（需后端 + Neo4j + OPEN_NEO4J=true）**

Run: `node local-dev/kg-e2e.mjs`
Expected: KG-S1..S9 全部 PASS

- [ ] **Step 3: 提交**

```bash
git add local-dev/kg-e2e.mjs
git commit -m "test(knowledge-graph): 后端 E2E 脚本"
```

---

## Task 10: 文档四件套

**Files:**
- Create: `docs/knowledgegraph/sdd.md`、`bdd.md`、`tdd.md`、`sop.md`
- Modify: `docs/README.md`（模块地图登记）

- [ ] **Step 1: 写 SDD/BDD/TDD/SOP**

按 [DOC-STANDARD.md](../../DOC-STANDARD.md) 与 `docs/wiki/*` 的结构编写；`bdd.md` 场景编号 `KG-S1..S9`（与 E2E 对齐），`tdd.md` 记录验证映射与结果（含 `dotnet build`/`dotnet test`/`kg-e2e.mjs`），`sdd.md` 引用本计划的设计与决策 D1..D10，`sop.md` 写运维排障（Neo4j 连不上、能力未开启、删除类型被拒）。

- [ ] **Step 2: 提交**

```bash
git add docs/knowledgegraph docs/README.md
git commit -m "docs(knowledge-graph): 模块四件套"
```

---

## Task 11: 测试项目（如 Task 3/5 之前未建）

**Files:**
- Create: `tests/MoAI.KnowledgeGraph.Tests/MoAI.KnowledgeGraph.Tests.csproj`
- Modify: `MoAI.sln`（可选）与 `Directory.Packages.props`（若缺 EF Sqlite）

- [ ] **Step 1: csproj**

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
    <NoWarn>$(NoWarn);CA1707;CS1591;SA1600</NoWarn>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" VersionOverride="10.0.11" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="Moq" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\database\MoAI.Database.Postgres\MoAI.Database.Postgres.csproj" />
    <ProjectReference Include="..\..\src\knowledgegraph\MoAI.KnowledgeGraph.Core\MoAI.KnowledgeGraph.Core.csproj" />
  </ItemGroup>

</Project>
```

- [ ] **Step 2: 构建并运行**

Run: `dotnet test tests/MoAI.KnowledgeGraph.Tests/MoAI.KnowledgeGraph.Tests.csproj`
Expected: PASS

- [ ] **Step 3: 提交**

```bash
git add tests/MoAI.KnowledgeGraph.Tests MoAI.sln
git commit -m "test(knowledge-graph): 单元测试项目"
```

---

## 自检

- **Spec 覆盖**：独立模块（Task 1）、模板 schema（Task 2/3/6）、手动节点边 CRUD（Task 4/7）、设置门禁（Task 5/6/7 的 `Enabled` 校验）、权限（Task 5 授权助手 + 各 Handler adminOnly）、API（Task 8）、E2E/文档（Task 9/10）。画布、文档抽取、知识库绑定按设计明确不在本期。
- **类型一致性**：`IKnowledgeGraphStore` 方法名在 Task 4 定义，Task 5/6/7 Handler 调用一致（`CountNodesByEntityTypeAsync`/`CountEdgesByRelationTypeAsync`/`GetNodeAsync`/`DeleteNodeAsync`/`CountNodesByEntityTypeAsync`）。`KnowledgeGraphEntity.Id` 为 `long`，全链路 `KgId: long` 一致。
- **遗留风险**：`Neo4jKnowledgeGraphStore.WriteCursorAsync` 的 `MaterializedCursor` 实现较脆，已给出 `WriteReadAsync` 替代方案，实施时优先采用替代方案。

---

## Task 12（补充）: 外部接入图谱（connected）

> 依据 [设计稿](../specs/2026-09-10-knowledge-graph-design.md) D11–D13。接入图 = 同一台 Neo4j 实例的某个 database，平台只登记 + 内省 schema，只读。

**Files（改）**
- `src/database/MoAI.Database.Shared/Entities/KnowledgeGraphEntity.cs` — 增 `Mode` / `Database`
- `src/database/MoAI.Database.Postgres/Data/KnowledgeGraphConfiguration.cs` — 映射两列
- `asserts/knowledge_graph.sql` — 增两列
- `.../Core/Services/IKnowledgeGraphStore.cs`、`Neo4jKnowledgeGraphStore.cs` — 增探活/内省
- `.../Core/Services/IKnowledgeGraphAuthorizer.cs`、`KnowledgeGraphAuthorizer.cs` — 增 `AuthorizeManagedAsync`
- `.../Shared/Models/KnowledgeGraphModes.cs`、`KnowledgeGraphIntrospection.cs`
- `.../Shared/Commands/CreateKnowledgeGraphCommand.cs`、`.../Core/Handlers/CreateKnowledgeGraphCommandHandler.cs`
- `.../Shared/Queries/Responses/*`（detail/list/schema）与对应 Handler
- `.../Core/Handlers/DeleteKnowledgeGraphCommandHandler.cs`
- 所有 schema / node / edge 写 Handler → 改调 `AuthorizeManagedAsync`
- `.../Api/Controllers/KnowledgeGraphController.cs`

- [ ] **Step 1: 实体与 DDL 增列**

`KnowledgeGraphEntity` 增：

```csharp
    /// <summary>
    /// 图谱来源：managed=平台托管，connected=外部接入.
    /// </summary>
    public string Mode { get; set; } = KnowledgeGraphModes.Managed;

    /// <summary>
    /// 接入的外部 Neo4j 数据库名（仅 connected）.
    /// </summary>
    public string? Database { get; set; }
```

（`using MoAI.KnowledgeGraph.Models;`）

`KnowledgeGraphConfiguration` 增：

```csharp
        builder.Property(e => e.Mode).HasMaxLength(20).HasDefaultValueSql("'managed'::character varying").HasComment("来源").HasColumnName("mode");
        builder.Property(e => e.Database).HasMaxLength(100).HasComment("接入数据库名").HasColumnName("database");
```

`asserts/knowledge_graph.sql` 的 `kg` 建表增 `mode varchar(20) NOT NULL DEFAULT 'managed'`、`database varchar(100) NULL`；对已有库追加：

```sql
ALTER TABLE public.kg ADD COLUMN IF NOT EXISTS mode varchar(20) NOT NULL DEFAULT 'managed';
ALTER TABLE public.kg ADD COLUMN IF NOT EXISTS database varchar(100) NULL;
```

- [ ] **Step 2: 常量与内省模型（Shared）**

`Models/KnowledgeGraphModes.cs`：

```csharp
namespace MoAI.KnowledgeGraph.Models;

/// <summary>
/// 图谱来源常量.
/// </summary>
public static class KnowledgeGraphModes
{
    /// <summary>
    /// 平台托管.
    /// </summary>
    public const string Managed = "managed";

    /// <summary>
    /// 外部接入.
    /// </summary>
    public const string Connected = "connected";

    /// <summary>
    /// 是否合法.
    /// </summary>
    /// <param name="mode">来源值.</param>
    /// <returns>合法返回 true.</returns>
    public static bool IsValid(string? mode)
        => mode == Managed || mode == Connected;
}
```

`Models/KnowledgeGraphIntrospection.cs`：

```csharp
namespace MoAI.KnowledgeGraph.Models;

/// <summary>
/// 外部图内省结果.
/// </summary>
/// <param name="Labels">标签及节点数.</param>
/// <param name="RelationshipTypes">关系类型及边数.</param>
/// <param name="PropertyKeys">属性键.</param>
public sealed record KnowledgeGraphIntrospection(
    IReadOnlyList<KnowledgeGraphIntrospectedItem> Labels,
    IReadOnlyList<KnowledgeGraphIntrospectedItem> RelationshipTypes,
    IReadOnlyList<string> PropertyKeys);

/// <summary>
/// 内省项.
/// </summary>
/// <param name="Name">名称.</param>
/// <param name="Count">数量.</param>
public sealed record KnowledgeGraphIntrospectedItem(string Name, long Count);
```

- [ ] **Step 3: 存储层增探活 / 内省**

`IKnowledgeGraphStore` 增：

```csharp
    /// <summary>
    /// 探活指定数据库.
    /// </summary>
    Task<bool> ProbeDatabaseAsync(string database, CancellationToken cancellationToken);

    /// <summary>
    /// 内省指定数据库的标签 / 关系类型 / 属性键.
    /// </summary>
    Task<KnowledgeGraphIntrospection> IntrospectAsync(string database, CancellationToken cancellationToken);
```

`Neo4jKnowledgeGraphStore` 增（需要 `using MoAI.KnowledgeGraph.Models;`）：

```csharp
    /// <inheritdoc/>
    public async Task<bool> ProbeDatabaseAsync(string database, CancellationToken cancellationToken)
    {
        try
        {
            var driver = await _provider.GetDriverAsync(cancellationToken);
            await using var session = driver.AsyncSession(b => b.WithDatabase(database));
            var cursor = await session.RunAsync("CALL db.labels()");
            await cursor.ConsumeAsync();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<KnowledgeGraphIntrospection> IntrospectAsync(string database, CancellationToken cancellationToken)
    {
        var driver = await _provider.GetDriverAsync(cancellationToken);
        await using var session = driver.AsyncSession(b => b.WithDatabase(database));

        var labelCursor = await session.RunAsync("CALL db.labels() YIELD label RETURN label ORDER BY label");
        var labels = new List<KnowledgeGraphIntrospectedItem>();
        foreach (var record in await labelCursor.ToListAsync())
        {
            var label = record["label"].As<string>();
            if (label.Contains('`', StringComparison.Ordinal))
            {
                labels.Add(new KnowledgeGraphIntrospectedItem(label, 0));
                continue;
            }

            long count;
            try
            {
                var countCursor = await session.RunAsync($"MATCH (n:`{label}`) RETURN count(n) AS c");
                var countRecord = (await countCursor.ToListAsync())[0];
                count = countRecord["c"].As<long>();
            }
            catch (Exception)
            {
                count = 0;
            }

            labels.Add(new KnowledgeGraphIntrospectedItem(label, count));
        }

        var relCursor = await session.RunAsync("CALL db.relationshipTypes() YIELD relationshipType RETURN relationshipType ORDER BY relationshipType");
        var relations = new List<KnowledgeGraphIntrospectedItem>();
        foreach (var record in await relCursor.ToListAsync())
        {
            var relType = record["relationshipType"].As<string>();
            if (relType.Contains('`', StringComparison.Ordinal))
            {
                relations.Add(new KnowledgeGraphIntrospectedItem(relType, 0));
                continue;
            }

            long count;
            try
            {
                var countCursor = await session.RunAsync($"MATCH ()-[r:`{relType}`]->() RETURN count(r) AS c");
                var countRecord = (await countCursor.ToListAsync())[0];
                count = countRecord["c"].As<long>();
            }
            catch (Exception)
            {
                count = 0;
            }

            relations.Add(new KnowledgeGraphIntrospectedItem(relType, count));
        }

        var keyCursor = await session.RunAsync("CALL db.propertyKeys() YIELD propertyKey RETURN propertyKey ORDER BY propertyKey");
        var keys = (await keyCursor.ToListAsync()).Select(x => x["propertyKey"].As<string>()).ToList();

        return new KnowledgeGraphIntrospection(labels, relations, keys);
    }
```

- [ ] **Step 4: 只读门禁**

`IKnowledgeGraphAuthorizer` 增：

```csharp
    /// <summary>
    /// 校验对“可写（托管）图谱”的访问；外部接入图谱抛 409.
    /// </summary>
    Task<(KnowledgeGraphEntity Graph, TeamRole Role)> AuthorizeManagedAsync(long kgId, bool adminOnly, CancellationToken cancellationToken);
```

`KnowledgeGraphAuthorizer` 实现：

```csharp
    /// <inheritdoc/>
    public async Task<(KnowledgeGraphEntity Graph, TeamRole Role)> AuthorizeManagedAsync(long kgId, bool adminOnly, CancellationToken cancellationToken)
    {
        var result = await AuthorizeAsync(kgId, adminOnly, cancellationToken);
        if (string.Equals(result.Graph.Mode, KnowledgeGraphModes.Connected, StringComparison.Ordinal))
        {
            throw new BusinessException("外部接入图谱为只读.") { StatusCode = 409 };
        }

        return result;
    }
```

把以下 Handler 中的 `AuthorizeAsync` 改为 `AuthorizeManagedAsync`：实体类型/关系类型增删改（6 个）、节点增删改（3 个）、边增删改（3 个）。schema 查询、节点/边查询保持 `AuthorizeAsync`（但节点/边查询在 connected 下不提供，前端不调用即可；如需强约束也可改 managed）。

- [ ] **Step 5: 创建/接入命令与 Handler**

`CreateKnowledgeGraphCommand` 增：

```csharp
    /// <summary>
    /// 来源：managed / connected.
    /// </summary>
    public string Mode { get; init; } = KnowledgeGraphModes.Managed;

    /// <summary>
    /// 接入的 Neo4j 数据库名（仅 connected）.
    /// </summary>
    public string? Database { get; init; }
```

`Validate` 增：

```csharp
        validate.RuleFor(x => x.Mode).Must(KnowledgeGraphModes.IsValid).WithMessage("图谱来源不合法.");
        validate.RuleFor(x => x.Database).NotEmpty().When(x => x.Mode == KnowledgeGraphModes.Connected).WithMessage("接入图谱必须填写数据库名.");
        validate.RuleFor(x => x.TemplateKey).Empty().When(x => x.Mode == KnowledgeGraphModes.Connected).WithMessage("接入图谱不能使用模板.");
```

`CreateKnowledgeGraphCommandHandler`：在 settings 校验后分支：

```csharp
        if (request.Mode == KnowledgeGraphModes.Connected)
        {
            var database = request.Database!.Trim();
            if (!await _store.ProbeDatabaseAsync(database, cancellationToken))
            {
                throw new BusinessException("数据库不存在或无法访问.") { StatusCode = 400 };
            }

            var connectedGraph = new KnowledgeGraphEntity
            {
                TeamId = (int)request.TeamId,
                Name = request.Name,
                Description = request.Description ?? string.Empty,
                TemplateKey = null,
                Mode = KnowledgeGraphModes.Connected,
                Database = database,
            };
            _databaseContext.KnowledgeGraphs.Add(connectedGraph);
            await _databaseContext.SaveChangesAsync(cancellationToken);
            return new SimpleLong { Value = connectedGraph.Id };
        }
```

Handler 需注入 `IKnowledgeGraphStore _store`（构造函数与字段同步增加）。托管分支沿用原逻辑并设 `Mode = KnowledgeGraphModes.Managed`、`Database = null`。

- [ ] **Step 6: 响应增字段 + schema 分支**

`KnowledgeGraphItem` / `QueryKnowledgeGraphCommandResponse` 增：

```csharp
    /// <summary>
    /// 来源：managed / connected.
    /// </summary>
    public string Mode { get; init; } = KnowledgeGraphModes.Managed;

    /// <summary>
    /// 接入数据库名（仅 connected）.
    /// </summary>
    public string? Database { get; init; }

    /// <summary>
    /// 是否只读（connected=true）.
    /// </summary>
    public bool ReadOnly { get; init; }
```

对应 `QueryKnowledgeGraphsCommandHandler` / `QueryKnowledgeGraphCommandHandler` 的 `Select`/返回补齐：`Mode = x.Mode, Database = x.Database, ReadOnly = x.Mode == KnowledgeGraphModes.Connected`。

`KnowledgeGraphEntityTypeItem` 改 `long? EntityTypeId`、增 `long? Count`；`KnowledgeGraphRelationTypeItem` 改 `long? RelationTypeId`、增 `long? Count`；`QueryKnowledgeGraphSchemaCommandResponse` 增：

```csharp
    /// <summary>
    /// 来源.
    /// </summary>
    public string Mode { get; init; } = KnowledgeGraphModes.Managed;

    /// <summary>
    /// 接入数据库名.
    /// </summary>
    public string? Database { get; init; }

    /// <summary>
    /// 是否只读.
    /// </summary>
    public bool ReadOnly { get; init; }

    /// <summary>
    /// 属性键（仅 connected）.
    /// </summary>
    public List<string> PropertyKeys { get; init; } = new();
```

`QueryKnowledgeGraphSchemaCommandHandler` 分支：

```csharp
        var (graph, _) = await _authorizer.AuthorizeAsync(request.KgId, adminOnly: false, cancellationToken);

        if (string.Equals(graph.Mode, KnowledgeGraphModes.Connected, StringComparison.Ordinal))
        {
            var introspection = await _store.IntrospectAsync(graph.Database!, cancellationToken);
            return new QueryKnowledgeGraphSchemaCommandResponse
            {
                Mode = KnowledgeGraphModes.Connected,
                Database = graph.Database,
                ReadOnly = true,
                EntityTypes = introspection.Labels.Select(x => new KnowledgeGraphEntityTypeItem { EntityTypeId = null, Name = x.Name, Color = string.Empty, Description = string.Empty, Count = x.Count }).ToList(),
                RelationTypes = introspection.RelationshipTypes.Select(x => new KnowledgeGraphRelationTypeItem { RelationTypeId = null, Name = x.Name, Color = string.Empty, Description = string.Empty, Count = x.Count }).ToList(),
                PropertyKeys = introspection.PropertyKeys.ToList(),
            };
        }
```

（Handler 需注入 `IKnowledgeGraphStore _store`。）托管分支补齐 `Mode/Database/ReadOnly`。

- [ ] **Step 7: 删除 Handler 分支**

`DeleteKnowledgeGraphCommandHandler`：`PurgeGraphAsync` 仅托管执行：

```csharp
        if (string.Equals(graph.Mode, KnowledgeGraphModes.Managed, StringComparison.Ordinal))
        {
            await _store.PurgeGraphAsync(graph.Id, cancellationToken);
        }
```

- [ ] **Step 8: Controller POST 传参**

Controller 的 `Create` 直接透传 `req`（已是命令类型），无需改；确认 `CreateKnowledgeGraphCommand` 的 `Mode`/`Database` 会随 body 绑定即可。

- [ ] **Step 9: 构建 + 单测 + E2E 补充**

- 单测：`CreateKnowledgeGraphCommandHandlerTests` 增「connected + 探活失败 400」「connected + 探活成功落库 Database」「managed 仍走模板」
- E2E `kg-e2e.mjs` 增：接入不存在库 400 → 接入真实库（可先用托管图所在默认库 `neo4j` 探活）→ `schema` 返回 `mode=connected` 且只读 → 对 connected 调 `nodes`（应 409 只读）
- 文档四件套补 `KG-S10..S12`

- [ ] **Step 10: 提交**

```bash
git add src/knowledgegraph src/database asserts local-dev tests docs
git commit -m "feat(knowledge-graph): 外部 Neo4j 图谱接入（只读 + schema 内省）"
```

