# 存储模块使用规范（S3 文件路径约定）

## 概述

MoAI 后端自身不存取本地文件，所有文件均依赖 S3（OSS）存储。文件上传、读取、删除
统一由 `MoAI.Storage` 模块提供能力，其他模块**不能**直接操作 `file` 表，也不能直接
访问 S3 接口，必须通过 `IStorageService`（领域服务）存取文件。

## 公开目录约定（固定规则）

需要**允许公开访问**的文件（例如用户头像、封面图片、团队头像等），即允许用户直接通过
web 服务中转访问（免登录、静态地址）的文件，**必须存储在以 `public/` 开头的目录下**。

存储桶 ObjectKey 约定：

```
public/{子目录}/{sha256}.{ext}
```

例如：

- 头像：`public/images/xxx.png`
- 封面：`public/cover/xxx.png`

### 为什么必须加 `public` 前缀

`/static/{objectKey}` 是免登录的公开访问入口。为安全起见，中间件会**校验**请求的
ObjectKey 是否位于 `public` 目录下；**不是** `public` 目录的文件会被拒绝访问（返回 404）。
因此打算公开的文件必须存放在 `public/` 下，非公开文件无需（也不应该）加该前缀。

## 其他模块如何正确使用

### 上传文件时

其他模块使用 `IStorageService` 上传 / 预上传文件时，如果该文件需要公开访问，
**必须自行拼接 `public` 前缀**。推荐使用 `FileStoreHelper.ToPublicObjectKey(key)`，
它会自动补全 `public/` 前缀：

```csharp
using MoAI.Storage.Helpers;
using MoAI.Storage.Services;

// 例如：生成图片公开 ObjectKey
var objectKey = FileStoreHelper.ToPublicObjectKey(
    FileStoreHelper.GetObjectKey(sha256: fileSha256, fileName: fileName, prefix: "images"));
// => "public/images/{sha256}.png"
```

调用示例：

```csharp
var result = await storageService.PreUploadAsync(new PreUploadFileCommand
{
    SHA256 = sha256,
    ContentType = contentType,
    FileSize = fileSize,
    ObjectKey = objectKey,   // 已包含 public/ 前缀
    Expiration = TimeSpan.FromMinutes(5)
}, cancellationToken);
```

### 获取公开访问地址

需要获取文件的公开静态地址（免登录、静态地址），调用
`IStorageService.GetPublicFileUrl(objectKey)`，传入**已经包含 `public/` 前缀**的
ObjectKey：

```csharp
var url = storageService.GetPublicFileUrl(objectKey).ToString();
// => "https://xxx.com/static/public/images/sha256.png"
```

注意：`GetPublicFileUrl` 只会基于传入的 ObjectKey 生成 `/static/...` 地址，不会自动补
`public` 前缀。**ObjectKey 必须由调用方规范地带上 `public/` 前缀**，这样生成的地址才能
通过中间件的校验并正常访问。

### 判断是否公开

```csharp
var isPublic = FileStoreHelper.IsPublicObjectKey(objectKey);
// objectKey 位于 public 目录下返回 true
```

## 公开与私有的区分

| 文件类型 | ObjectKey 示例 | 是否可通过 `/static` 访问 |
| --- | --- | --- |
| 公开（头像、封面等） | `public/images/xxx.png` | 是（免登录、静态地址） |
| 私有（默认） | `doc/xxx.pdf` | 否（被中间件拒绝，返回 404） |

## 目录层实现位置

- 路径约定常量与工具：`MoAI.Storage.Shared/Helpers/FileStoreHelper.cs`
- `/static` 公开访问中间件：`MoAI.Storage.S3/Middlewares/StorageStaticFilesMiddleware.cs`
- 领域服务接口：`MoAI.Storage.Shared/Services/IStorageService.cs`
- 领域服务实现：`MoAI.Storage.S3/Services/StorageService.cs`
