# 设置项（Settings）编写规范

设置模块（`MoAI.Settings`）面向 `setting` 表，提供一组**固定**的系统配置项。配置项的**定义在内置，可调整的是值**。

## 模块架构

```
src/settings/
├── MoAI.Settings.Shared/     # 共享层 - Command、Query、响应模型、服务接口
├── MoAI.Settings.Api/        # API 层 - SettingsController（权限在接口层判断）
└── MoAI.Settings.Core/       # 核心层 - SettingsService、SettingDefinitions
```

相关文件：

| 文件 | 作用 |
| --- | --- |
| `src/database/MoAI.Database.Shared/Entities/SettingEntity.cs` | `setting` 表实体 |
| `src/database/MoAI.Database.Postgres/Data/SettingConfiguration.cs` | 表结构配置（字段长度、索引） |
| `src/database/MoAI.Database.Shared/Seed/SettingDefinition.cs` | 设置项定义模型 |
| `src/database/MoAI.Database.Shared/Seed/SettingDefinitions.cs` | **内置设置项注册表** |
| `src/database/MoAI.Database.Shared/Seed/SettingSeed.cs` | 设置种子数据（枚举 `SettingDefinitions` 自动生成） |
| `src/database/MoAI.Database.Shared/Seed/UserSeed.cs` | 用户种子数据 |
| `src/database/MoAI.Database.Shared/Seed/ClassifySeed.cs` | 分类种子数据 |
| `src/database/MoAI.Database.Shared/DatabaseContext.Configuration.cs` | `SeedData` 编排（仅调用各静态种子） |
| `src/settings/MoAI.Settings.Core/Services/SettingsService.cs` | 领域服务（校验 key、查询/写入） |
| `src/settings/MoAI.Settings.Api/Controllers/SettingsController.cs` | 接口，前置权限判断 |

## SettingDefinitions（内置设置项注册表）

`SettingDefinitions`（`MoAI.Database.Shared/Seed/SettingDefinitions.cs`，命名空间 `MoAI.Database.Seed`）是系统所有配置项的**单一事实来源**。它既用于校验保存时的 key 是否合法，也用于 GET 查询在数据库无记录时返回默认值，以及首次写入时初始化 `Name` / `Description`，同时直接供 `SettingSeed` 读取以生成种子数据（即 `BackingField`）。

每个配置项通过 `SettingDefinition` 描述：

```csharp
public sealed class SettingDefinition
{
    public string Key { get; init; }          // 唯一 key，与 setting 表 key 列一致
    public string Name { get; init; }         // 配置名称（内置，不随用户修改）
    public string Description { get; init; }  // 描述（内置，不随用户修改）
    public string DefaultValue { get; init; } // 默认值，数据库无记录时使用
}
```

`SettingDefinitions` 暴露两个成员：

- `SettingDefinitions.All`：全部内置设置项（只读列表）。
- `SettingDefinitions.Find(string key)`：按 key 查找，未命中返回 `null`。

将 key 提取为常量便于复用（例如 `SettingDefinitions.OAuthAutoRegisterKey`）。

## 新增一个设置项的步骤

> 前端不是动态渲染，新增配置项需要在设置页 / 接口同步加固定字段。

1. **定义内置项** —— 在 `SettingDefinitions` 添加一条 `SettingDefinition`，并为其 key 定义常量。
2. **种子数据自动生成** —— `SettingSeed.Apply` 会遍历 `SettingDefinitions` 生成对应的 `SettingEntity` 种子记录，无需手工改动 `SeedData`。
3. **添加前端固定字段** —— 在 `ui/src/pages/settings/Settings.tsx` 增加对应的固定表单字段与 `ui/src/i18n/locales/{zh-CN,en-US}/common.json` 文案。
4. **（可选）接入业务** —— 需要读取配置的业务代码直接查询 `setting` 表的 key 即可。

## 编写规则

### key 命名

- 使用**小写加下划线**（snake_case），如 `oauth_auto_register`。
- 每个 key 定义为一个 `const string`，避免硬编码字符串散落各处。
- key 全局唯一，删除该配置项等于移除其定义 + 种子数据。

### 字段长度（来自 `SettingConfiguration`）

| 字段 | 最大长度 | 说明 |
| --- | --- | --- |
| `key` | 50 | 唯一，哈希索引 `setting_key_index` |
| `name` | 20 | 配置名称 |
| `description` | 255 | 描述 |
| `value` | 2000 | 配置值（json/generic string） |

### value 约定

- `value` 以字符串存储，可承载简单类型或 JSON 文本。
- 布尔型统一使用 `"true"` / `"false"`（首写默认 `"false"`）。
- `DefaultValue` 在数据库无记录时由 GET 返回，应始终保证类型合法。

### 保存校验与首次写入

保存逻辑位于 `SettingsService.SaveSettingAsync`：

1. 校验 `key` 是否在 `SettingDefinitions` 白名单内，不在则抛：
   ```csharp
   throw new BusinessException("无效的配置项.") { StatusCode = 400 };
   ```
2. key 合法但数据库无该记录 → 自动用内置 `Key` / `Name` / `Description` 插入一条新记录。
3. key 合法且记录存在 → 仅更新 `value`。

因此即使种子数据未写入（例如数据库已存在、仅使用 `EnsureCreated()` 建库），首次保存也会自动创建。

### 权限控制（在接口层判断）

权限在 **Controller** 层判断，**禁止**在 Handler 或领域服务 `SettingsService` 中做用户角色/权限判断。示例：

```csharp
[HttpGet]
public async Task<QuerySettingsCommandResponse> QuerySettings(CancellationToken ct)
{
    var userState = await _userAccountService.GetUserStateAsync(_userContextProvider.GetUserContext().UserId, ct);
    if (!userState.IsAdmin && !userState.IsRoot)
    {
        throw new BusinessException("只有管理员可以访问设置项") { StatusCode = 403 };
    }

    return await _mediator.Send(new QuerySettingsCommand(), ct);
}

[HttpPut]
public async Task<EmptyCommandResponse> SaveSetting([FromBody] SaveSettingCommand req, CancellationToken ct)
{
    var userState = await _userAccountService.GetUserStateAsync(_userContextProvider.GetUserContext().UserId, ct);
    if (!userState.IsRoot)
    {
        throw new BusinessException("只有超级管理员可以修改设置项") { StatusCode = 403 };
    }

    return await _mediator.Send(req, ct);
}
```

- **访问（GET）**：`IsAdmin` 或 `IsRoot`，否则 `403`。
- **修改（PUT）**：仅 `IsRoot`（超级管理员），否则 `403`。
- 其中 `IsRoot`（`setting` 表 `key="root"` 的值等于当前用户 id）由 `UserAccountService.GetUserStateAsync` 填充到 `UserStateInfo.IsRoot`。
- 认证由 `ApiApplicationModelConvention` 自动为 Controller 追加 `[Authorize]`，无需手写。

## 示例（内置：允许第三方账号登录直接创建账号）

`SettingDefinitions`：

```csharp
public const string OAuthAutoRegisterKey = "oauth_auto_register";

new SettingDefinition
{
    Key = OAuthAutoRegisterKey,
    Name = "允许第三方账号登录直接创建账号",
    Description = "开启后，第三方授权登录（例如 GitHub、Google 等）在未注册时将自动创建账号.",
    DefaultValue = "false"
}
```

种子数据由 `SettingSeed` 自动生成（`MoAI.Database.Shared/Seed/SettingSeed.cs`）：

```csharp
public static void Apply(ModelBuilder modelBuilder)
{
    var settings = new List<SettingEntity>
    {
        new SettingEntity { Id = 1, Key = "root", Value = "1", Description = "超级管理员" }
    };

    int settingId = 2;
    foreach (var definition in SettingDefinitions.All)
    {
        settings.Add(new SettingEntity
        {
            Id = settingId++,
            Key = definition.Key,
            Name = definition.Name,
            Description = definition.Description,
            Value = definition.DefaultValue
        });
    }

    modelBuilder.Entity<SettingEntity>().HasData(settings);
}
```

`DatabaseContext.SeedData` 仅作编排：

```csharp
protected static void SeedData(ModelBuilder modelBuilder)
{
    UserSeed.Apply(modelBuilder);
    ClassifySeed.Apply(modelBuilder);
    SettingSeed.Apply(modelBuilder);
}
```

> 注意：`key="root"` 为系统级配置（超级管理员），不通过 `SettingDefinitions` 暴露，不应作为普通设置项读写。
