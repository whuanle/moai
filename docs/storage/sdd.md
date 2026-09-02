# 文件存储（Storage，STO）设计规格（SDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md)（@STO-Sxx） ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 上游：[../infra/sdd.md](../infra/sdd.md)（S3 配置来源） ｜ 证据：[local-dev/audit-storage.mjs](../../local-dev/audit-storage.mjs)
> 规范：[../DOC-STANDARD.md](../DOC-STANDARD.md)；ObjectKey 路径规范必读 [../storage-file-layout.md](../storage-file-layout.md)。行为场景见 BDD，本文不重复。

## 目标

后端不存取本地文件，统一依赖 S3 兼容对象存储（本地 MinIO，容器 `moai-minio`，endpoint `http://127.0.0.1:9000`，桶 `moai`；线上可换任意 S3 兼容服务）。本模块提供：

1. **预签名上传流程**（预上传 → 浏览器直传 OSS → 完成校验），后端不经手文件流；
2. **领域服务 `IStorageService`**：其他模块存取文件的唯一入口（禁止直连 `file` 表或 OSS）；
3. **公开静态中转** `/static/{objectKey}`：免登录展示头像/图标等公开资源，前端不接触 OSS 地址与签名。

## 配置

`SystemOptions.Storage`（`MoAI:Storage` 节，见 [../infra/sdd.md](../infra/sdd.md)）：Endpoint / ForcePathStyle（MinIO 必须 true）/ Bucket / AccessKeyId / AccessKeySecret。底层 AWS SDK `AmazonS3Client`（ServiceURL + ForcePathStyle）。

## 数据模型

表 `file`（`FileEntity`，IFullAudited）：Id(long) / ObjectKey（唯一，`{prefix}/{sha256}.{ext}`）/ FileExtension / FileSha256 / FileSize / ContentType / IsUploaded（预上传 false → 完成 true）。**ObjectKey 是幂等键**：相同 sha256+名称+前缀得到相同 key，秒传复用同一条记录。

## 组件

```
src/storage/
├── MoAI.Storage.Shared/   Services/IStorageService、Helpers/FileStoreHelper（ObjectKey/public 前缀/SHA256/MIME）
│                          Commands/（PreUpload{File,Image,TempFile}、CompleteFileUpload、UploadStreamFile、DeleteFile）
│                          Models/（响应与结果模型）
├── MoAI.Storage.Core/     StorageCoreModule（模块装配）
└── MoAI.Storage.S3/       Services/S3Client（AWSSDK：预签名 PUT/GET、TransferUtility、删除、读取、大小）
                          Services/StorageService（file 表登记 + 状态机）
                          Handlers/（PreUploadImage / PreUploadTempFile / CompleteFileUpload）
                          Controllers/（PublicController：/storage/public；CompleteUploadController：/storage/complate_url）
                          Middlewares/StorageStaticFilesMiddleware（/static/{objectKey}，宿主 src/MoAI 全局注册）
ui/src/utils/storage.ts    resolveStorageUrl / uploadImageWithKey / uploadImage
```

## HTTP 契约（登录态，无角色门禁）

| 方法 | 路由 | 说明 |
|---|---|---|
| POST | `/storage/public/pre_upload_image` | 公开图片，ObjectKey=`public/images/{sha256}.{ext}` |
| POST | `/storage/public/pre_upload_temp` | 临时文件，ObjectKey=`temp/{sha256}.{ext}`（非公开） |
| POST | `/storage/complate_url` | 完成上传回调（历史拼写 complate） |

预上传响应：`isExist`（秒传）/`fileId`/`objectKey`/`uploadUrl`（秒传为空）/`expiration`；完成响应：`fileId`/`objectKey`/`accessUrl`（公开文件为 `/static` 地址，私有为 null）。

## 关键决策

1. **状态机**（[@STO-S1](./bdd.md#sto-s1)、[@STO-S7](./bdd.md#sto-s7)）：IsUploaded=true → 秒传不签 URL；无记录 → 登记 IsUploaded=false；记录超 5 分钟未完成 → 废弃（删对象+删行）重建；未超时 → 复用记录刷新 UpdateTime。
2. **完成校验**：fileId 不存在 404；已完成幂等返回；非本人未过期 409「其他用户正在上传此文件」、已过期废弃后 409「上传已过期，请重新上传」；isSuccess=false 清理；对象缺失或**大小**与登记不符 409「上传的文件已损坏」并清理。
3. **SHA-256 仅作 ObjectKey 组成与登记值**，完成校验比对 S3 侧文件大小而非哈希。
4. 预上传地址与记录有效期均 **5 分钟**（Handler 固定 Expiration；`PreUploadTimeout`=5 分钟）。
5. `/static` 中转：仅 GET/HEAD（405）；仅放行 `public/` 前缀（其余 404，安全边界）；读要求 IsUploaded=true；ContentType 用登记值（兜底 octet-stream）、`Cache-Control: public,max-age=86400`、ETag=ObjectKey 的 SHA-256；IO 异常按 404、其他 500。
6. 领域服务：`PreUploadAsync` / `UploadStreamAsync`（流式直传+直接标记完成，幂等）/ `CompleteAsync` / `DeleteFilesAsync` / `GetDownloadUrl(s)Async` / `FileExistsAsync` / `ReadAsync` / `GetPublicFileUrl` / `GetFileInfoAsync`。
7. 前端 `uploadImageWithKey`：WebCrypto 算 SHA-256 → 预上传 → 非秒传 `fetch PUT` 直传（带 Content-Type）→ 完成 → 返回 `{objectKey,url}`；数据库只存 ObjectKey，展示经 `resolveStorageUrl`（绝对 URL 原样，否则拼 `{serviceUrl}/static/{objectKey}`）。头像（account）与 OAuth 图标（[../oauthconnect/sdd.md](../oauthconnect/sdd.md)）共用此链路。

## 已知问题

- 路由 `complate_url` 为历史拼写（complete），契约已定不改。
- 完成校验比对大小而非哈希：同长度不同内容的直传无法被检测（设计取舍，见决策 3）。
- 预上传废弃记录无后台任务：孤儿对象仅在下一次同 ObjectKey 预上传/完成时被顺带清理，无引用残留需在 MinIO 侧定期审计（见 [SOP](./sop.md)）。
- 本模块 CQRS Handler 按现状落在 S3 工程 `Handlers/`（无独立 Core 工程），与 [../cqrs-conventions.md](../cqrs-conventions.md) 分层略有偏离。
