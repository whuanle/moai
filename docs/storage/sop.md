# 文件存储（Storage，STO）操作手册（SOP）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md)（验收场景编号） ｜ [TDD](./tdd.md)（自动化映射） ｜ [SOP](./sop.md)

## 1. 环境与配置

| 项 | 说明 |
|---|---|
| 对象存储 | S3 兼容（本地 MinIO，容器 `moai-minio`，endpoint `http://127.0.0.1:9000`） |
| 桶 | `moai`（须提前创建） |
| 寻址风格 | ForcePathStyle=true（MinIO 必须路径风格） |
| 配置位置 | `MoAI:Storage` 节：Endpoint/Bucket/AccessKeyId/AccessKeySecret/ForcePathStyle（管理见 [../infra/sop.md](../infra/sop.md)） |

路径布局与 `public/` 前缀规范见 [../storage-file-layout.md](../storage-file-layout.md)。

## 2. 日常操作

| 操作 | 步骤 | 对应场景 |
|---|---|---|
| 新环境初始化 | ① 启动 MinIO 并建桶（`mc mb local/moai` 或控制台）；② 创建 AccessKey 写入 `MoAI:Storage`；③ 跑 [TDD 回归命令](./tdd.md) 走一遍全链路 | [@STO-S1](./bdd.md#sto-s1)~[@STO-S7](./bdd.md#sto-s7) |
| 上传报"文件损坏/过期" | 预签名地址与记录有效期均 **5 分钟**：直传须在窗口内完成并回调；超时记录自动废弃，重新发起即可，无需人工清理 | [@STO-S10](./bdd.md#sto-s10)、[@STO-S14](./bdd.md#sto-s14) |
| 清理文件 | 业务删除走 `IStorageService.DeleteFilesAsync`（删记录+删对象）；孤儿对象在下次同 ObjectKey 预上传/完成时自动清理，无引用的定期在 MinIO 侧审计 | [@STO-S21](./bdd.md#sto-s21) |

## 3. 排障

| 现象 | 原因 | 处理 |
|---|---|---|
| 预上传/完成 500，日志见 AmazonS3 异常 | S3 配置错误（Endpoint/AccessKey 错、ForcePathStyle 未开） | 核对 `MoAI:Storage` |
| 预签名地址可用但直传 404/403 | 桶不存在或凭据无权限 | `mc mb` 建桶；核对 AccessKey 策略 |
| 直传成功但完成 409「上传的文件已损坏」 | 直传内容大小与登记 fileSize 不一致 | 重新发起完整链路（[@STO-S10](./bdd.md#sto-s10)） |
| 完成 409「上传已过期」 | 预上传超 5 分钟未完成 | 重新预上传（[@STO-S14](./bdd.md#sto-s14)） |
| 完成 409「其他用户正在上传此文件」 | 另一用户持同一 objectKey 未完成记录 | 等待其完成/过期后重试（[@STO-S13](./bdd.md#sto-s13)） |
| `/static/...` 404 | key 不带 `public/` 前缀、未完成上传或对象缺失 | 公开文件必须走 `public/` 前缀（[@STO-S16](./bdd.md#sto-s16)/[@STO-S17](./bdd.md#sto-s17)） |
| 预签名 URL 浏览器不可用 | endpoint 为容器内网地址或协议不符 | Endpoint 须浏览器可达（本地 `http://127.0.0.1:9000`） |
| 头像上传后不显示 | serviceUrl 未取到（serverInfo 缺失） | 检查 `/api/common/serverinfo` 与前端 Env.serverUrl |

## 4. 验收流程（发布前）

1. 自动化：跑 [TDD 回归命令](./tdd.md)（audit-storage.mjs 7 断言）。
2. HTTP 手动走查：匿名预上传应 401（[@STO-S4](./bdd.md#sto-s4)）；完成不存在 fileId 应 404、失败清理与重复完成幂等（[@STO-S11](./bdd.md#sto-s11)/[@STO-S9](./bdd.md#sto-s9)/[@STO-S12](./bdd.md#sto-s12)）；POST `/static/...` 应 405（[@STO-S18](./bdd.md#sto-s18)）。
3. 浏览器走查：账号设置页更换头像成功并回显（[@STO-S22](./bdd.md#sto-s22)）；OAuth 连接器图标上传后表格内 `/static/public/images/…` 正常展示（[@STO-S23](./bdd.md#sto-s23)）。

## 5. 历史验收存档（L3 证据，保留原始记录）

- **2026-09-01 初版（回溯整理）**：功能此前已实现；旧 sop 内联 Node 脚本完整走「预上传→直传→完成→/static 访问→秒传→非 public 404」通过。
- **2026-09-01 第二轮·全系统深度测试**：深度 API 68/68（含存储全链路）；修复「avatar 伪造 objectKey → file 表校验 404」（上游 [../account/sdd.md](../account/sdd.md)）。
- **2026-09-02 终审 B**：[audit-storage.mjs](../../local-dev/audit-storage.mjs) 7/7（随机内容避免秒传干扰，PUT 直传不带 Authorization、字节级比对匿名访问、复用 fileId 断言）。

## 6. 变更记录

| 日期 | 变更 |
|---|---|
| 2026-09-01 | 初版（回溯整理）；两轮验收（见存档） |
| 2026-09-02 | 终审 B 7/7；按 [DOC-STANDARD](../DOC-STANDARD.md) 重构四件套 |
