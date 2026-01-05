# 插件授权管理功能

## 概述

插件授权管理功能允许管理员对私有插件（`IsPublic = false` 且 `TeamId = 0`）进行授权管理，控制哪些团队可以使用这些插件。

## 功能特性

- 只能授权私有且不属于任何团队的插件（`IsPublic = false` 且 `TeamId = 0`）
- 支持查询所有插件及其授权的团队列表
- 支持查询所有团队及其授权的插件列表
- 支持修改某个插件的授权团队列表（覆盖式更新）
- 支持批量授权插件给某个团队
- 支持批量撤销某个团队的插件授权
- 所有操作需要管理员权限

## API 接口

### 1. 查询所有插件及其授权的团队列表

**接口**: `POST /plugin/authorization/plugins`

**请求体**: 
```json
{}
```

**响应**:
```json
{
  "plugins": [
    {
      "pluginId": 1,
      "pluginName": "my-plugin",
      "title": "我的插件",
      "description": "插件描述",
      "isPublic": false,
      "authorizedTeams": [
        {
          "teamId": 1,
          "teamName": "团队A",
          "authorizationId": 1
        }
      ]
    }
  ]
}
```

### 2. 查询所有团队及其授权的插件列表

**接口**: `POST /plugin/authorization/teams`

**请求体**: 
```json
{}
```

**响应**:
```json
{
  "teams": [
    {
      "teamId": 1,
      "teamName": "团队A",
      "authorizedPlugins": [
        {
          "pluginId": 1,
          "pluginName": "my-plugin",
          "title": "我的插件",
          "authorizationId": 1
        }
      ]
    }
  ]
}
```

### 3. 修改某个插件的授权团队列表

**接口**: `POST /plugin/authorization/plugin/update`

**请求体**: 
```json
{
  "pluginId": 1,
  "teamIds": [1, 2, 3]
}
```

**说明**: 覆盖式更新，会删除不在列表中的授权，添加新的授权

### 4. 批量授权插件给某个团队

**接口**: `POST /plugin/authorization/team/authorize`

**请求体**: 
```json
{
  "teamId": 1,
  "pluginIds": [1, 2, 3]
}
```

### 5. 批量撤销某个团队的插件授权

**接口**: `POST /plugin/authorization/team/revoke`

**请求体**: 
```json
{
  "teamId": 1,
  "pluginIds": [1, 2, 3]
}
```

## 代码结构

### Shared 层 (MoAI.Plugin.Shared)

```
Authorization/
├── Commands/
│   ├── BatchAuthorizePluginsToTeamCommand.cs
│   ├── BatchRevokePluginsFromTeamCommand.cs
│   └── UpdatePluginAuthorizationsCommand.cs
└── Queries/
    ├── QueryPluginAuthorizationsCommand.cs
    ├── QueryTeamAuthorizationsCommand.cs
    └── Responses/
        ├── AuthorizedPluginItem.cs
        ├── AuthorizedTeamItem.cs
        ├── PluginAuthorizationItem.cs
        ├── QueryPluginAuthorizationsCommandResponse.cs
        ├── QueryTeamAuthorizationsCommandResponse.cs
        └── TeamAuthorizationItem.cs
```

### Core 层 (MoAI.Plugin.Core)

```
Authorization/
├── Handlers/
│   ├── BatchAuthorizePluginsToTeamCommandHandler.cs
│   ├── BatchRevokePluginsFromTeamCommandHandler.cs
│   └── UpdatePluginAuthorizationsCommandHandler.cs
└── Queries/
    ├── QueryPluginAuthorizationsCommandHandler.cs
    └── QueryTeamAuthorizationsCommandHandler.cs
```

### Api 层 (MoAI.Plugin.Api)

```
Controllers/
└── PluginAuthorizationController.cs
```

## 权限控制

所有接口都需要管理员权限，通过 `CheckIsAdminAsync` 方法验证。

## 业务规则

1. 只能授权私有且不属于任何团队的插件（`IsPublic = false` 且 `TeamId = 0`）
2. 授权时会自动验证团队是否存在
3. 授权时会自动过滤掉已存在的授权记录
4. 撤销授权时会删除对应的授权记录
5. 更新授权时采用覆盖式更新，会先删除不在列表中的授权，再添加新的授权
