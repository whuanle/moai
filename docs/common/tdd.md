# 公共领域（Common）验证映射（TDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md)（场景定义） ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md)
> 本文只做「场景 → 验证物 → 结果」映射；场景语义见 BDD 编号，设计依据见 SDD。本模块验证物为 curl/node 一行命令（无独立脚本）。

## 场景映射表

| 场景 | 验证物 | 结果（日期） |
|---|---|---|
| @COM-S1 | `curl -s http://127.0.0.1:5210/api/common/serverinfo` | PASS：五字段齐全（name=MoAI、serviceUrl、publicStoreUrl=`…/static`、rsaPublic 392 字符、maxUploadFileSize=104857600）（2026-09-01） |
| @COM-S2 | 以 serverinfo 的 rsaPublic 用 node crypto `publicEncrypt(PKCS1)` 加密 admin 密码登录（取到 accessToken）；另 `node local-dev/user-management-e2e.mjs` 34/34 | PASS（2026-09-01） |
| @COM-S3 | `node -e`：读 `{AppPath}/configs/rsa_private.key` 导出 SPKI DER Base64 与 `rsaPublic` 比对（完整命令见 SOP 第 2 节一致性自检） | PASS：逐字节一致（2026-09-01） |
| @COM-S4、@COM-S5 | 匿名/带 token 各 curl 一次 build_guid | PASS：匿名 401；带 token 200（GUID v7），两次值不同（2026-09-01） |
| @COM-S6 | @manual 浏览器走查（登录页网络面板） | PASS（2026-09-01） |
| @COM-S7 | @manual（缺陷确认：上传入口未接入体积预校验） | 现状记录（2026-09-01） |

## 回归命令

```bash
curl -s http://127.0.0.1:5210/api/common/serverinfo          # @COM-S1
curl -s -o /dev/null -w '%{http_code}\n' http://127.0.0.1:5210/api/common/build_guid  # @COM-S4 应 401
dotnet build src/MoAI/MoAI.csproj                            # 0 错误（58 个既有警告非本模块引入）
```

## 覆盖率说明

- 5 个命令验证场景 + 2 个手动；@COM-S7 为缺陷现状记录，接入校验后须改为断言式验证。
- 无独立自动化脚本；若纳入持续回归建议补 local-dev 脚本（当前以 SOP 人工命令兜底）。
