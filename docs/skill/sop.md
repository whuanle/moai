# 技能（Skill）模块运维规程（SOP）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ 沙箱配置：[../ai/sop.md](../ai/sop.md)

## 新增内置技能（开发者）

1. 脚本放 `src/skill/MoAI.Skill.Core/Resources/skills/{key}/`（仅白名单扩展名，csproj EmbeddedResource 自动收录）。
2. `BuiltinSkills.GetFiles` 登记 `{key}` 与文件清单（新增 case）。
3. `SkillSeed` 增加种子行（**固定 Guid**、key 蛇形、Instructions 写明调用步骤，路径用 `/workspace/skills/{key}/`）。
4. 重建库后自动生效；已有库需手工 INSERT 或重跑删库重建（EnsureCreated 不补种子）。

## 自定义技能（登录用户）

个人技能在 `/skills`「我的技能」Tab、团队技能在团队详情「团队技能」分区维护：标识（蛇形、创建后不可改）→ 上传包文件（文件名即包内路径）→ 填说明与描述。文件先 preupload（SHA256 去重）→ PUT 预签名 → complete → 保存时校验 fileId 已上传。删除仅自定义技能；内置技能只能禁用。市场上架：卡片「申请上架」→ 平台管理员在「审批上架」通过后进入 `/skill-market`。

## 沙箱（技能执行的前提）

- 全局：`MoAI:OpenSandBox`（Address/ApiKey/Image/TimeoutSeconds/RenewThresholdSeconds）。
- 本地开发参考：`uvx opensandbox-server`（`~/.sandbox.toml`：port=55900、api_key、`[docker] network_mode="bridge"` + `host_ip="127.0.0.1"`）；镜像 `opensandbox/code-interpreter:v1.1.0`、execd `opensandbox/execd:v1.0.22`。
- 应用级：AppManage「启用沙箱」写 `execution_settings.sandbox`。
- **镜像未预装 python-docx/python-pptx**：技能脚本 `_ensure` 自动 `pip install --break-system-packages`（需沙箱出站网络；默认无策略=放开）。若后续收紧网络，需放行 pypi.org 或改自建镜像。
- 未配置/未启用沙箱时：技能仍可挂载与加载说明，但脚本不执行（工具结果有明确提示）。

## 154 部署要点（154.8.214.31，SSH 密钥 `~/.ssh/sms_ci_deploy`，ubuntu）

1. 本地交叉发布产物打包 → `/tmp/moai154-app.tar.gz` 上传 → 替换 `~/moai154/app/`（先备份旧目录与 DB：`app.bak-*`、`backup-*.sql`）。
2. **DB schema 变更必须删库重建**（EnsureCreated 不做增量）：
   `docker exec moai154-postgres psql -U postgres -d moai -c "DROP SCHEMA public CASCADE; CREATE SCHEMA public; GRANT ALL ON SCHEMA public TO postgres; GRANT ALL ON SCHEMA public TO public;"`
3. `docker compose build moai && docker compose up -d moai`；启动即建表+种子。
4. 审计（服务器内）：serverinfo/login（openssl pkeyutl RSA 加密）→ `/api/skill/options|list` → `/api/ai/plugin/run`。
5. **154 不部署 OpenSandbox**（整机 1.9G 内存，code-interpreter 容器跑不动）：沙箱执行类功能在该环境不可用；如需端到端验证技能生成，用本地沙箱或换更高配实例。
6. 已知噪音：容器启动日志出现一次 `libgssapi_krb5.so.2` 缺失告警（.NET HTTP 栈 Kerberos 探测，非致命；镜像内禁 apt，勿尝试安装）。

## 故障排查

| 症状 | 处置 |
|---|---|
| 技能工具未出现在 list_tools | 应用是否挂载且技能未禁用；`SkillAppToolProvider` Order=14 装配日志 |
| 加载提示未启用沙箱 | AppManage 开启沙箱 + 后端配置 `MoAI:OpenSandBox.Address` |
| 脚本执行报 externally-managed | 技能脚本 `_ensure` 是否带 `--break-system-packages` |
| 产物下载 403/过期 | 预签名 1 小时；重新让 Agent 生成或再调 save_artifact |
| 技能/文档下载在浏览器内联打开 | 2026-09-17 起 `GetDownloadUrlAsync(fileName)` 已带 `response-content-disposition: attachment`；若复现检查对象存储是否透传 response-* 查询参数覆盖 |
| skill 表 id 非默认 uuid | 检查 `SkillConfiguration` 被 rescaffold 覆盖（Guid 主键 + `HasDefaultValueSql("uuid_generate_v4()")`） |

## 存量库 schema 变更（2026-09-16 增量）

- `skill` 补列：`is_public boolean not null default false`（已并入 [asserts/skill.sql](../../asserts/skill.sql)，幂等）。
- 新表 `app_user_config`：[asserts/app_user_config.sql](../../asserts/app_user_config.sql)（幂等，含 (app_id,user_id) partial 唯一索引）。
- 存量库执行：`docker exec -i moai-postgres psql -U postgres -d moai < asserts/skill.sql && docker exec -i moai-postgres psql -U postgres -d moai < asserts/app_user_config.sql`；**EnsureCreated 不会改已有库**，跳过此步会导致运行时查询 42P01/42703。

## 技能维护权限速查（2026-09-16 起）

| 操作 | 平台内置 | 团队技能 | 个人技能 |
|---|---|---|---|
| 创建 | —（代码内嵌） | 团队 Admin/Owner（`POST /api/skill` teamId>0） | 任意登录用户（teamId=0） |
| 更新/删除 | 禁止（仅可平台管理员禁用） | 团队 Admin/Owner | 归属人 |
| 禁用 | 平台管理员 | 团队 Admin/Owner | 归属人 |
| 绑定应用配置 | ✅ | ✅ 本团队应用 | ❌（个人技能仅限本人「应用设置」自选） |
| 上架市场 | —（不可上架/下载，脚本随平台分发） | 团队 Admin/Owner 申请，admin 审批 | 归属人申请，admin 审批 |

- 普通用户自选技能：对话页右上「应用设置」面板（`ControlOutlined` 按钮），保存 `PUT /api/app/{id}/userconfig`；专家=新会话默认，会话级「专家」面板可覆盖。

## 回归入口

```bash
node local-dev/skill-userconfig-e2e.mjs      # SKL 20 场景（归属权限 + 用户配置）
node local-dev/skill-market-e2e.mjs          # SM 28 场景（市场列表/详情/下载可见性 + 上架审批 + 删除联动）
```
