# Wiki API Kiota 迁移设计

## 背景

`src/api/wiki.ts` 的知识库基础 CRUD 已使用 `getApiClient()`，但文档管理、内容提取、切割、向量化和模型配置仍通过手写 `authedFetch` 请求后端。手写请求复制了 base URL 和 Bearer token 逻辑，却没有经过 `FilterRequestHandler`，导致统一的 401 处理和 `feedback.handleError` 不生效。页面又普遍以“错误已由全局请求中间件统一提示”为前提吞掉异常，因此后端失败时可能没有任何用户提示。

当前生成的 `src/api/client` 已覆盖这些 Wiki 端点，不需要保留第二套后端 HTTP 请求实现。

## 目标

- `src/api/wiki.ts` 中所有 MoAI 后端请求统一通过 `getApiClient()` 和生成的 Kiota request builder 发起。
- 保持 `src/api/wiki.ts` 现有导出函数及页面调用契约稳定。
- 后端非 2xx、401 和网络异常统一经过 `src/api/kiota.ts` 的中间件处理并继续向调用方抛出。
- 页面现有的成功提示和 loading 状态保持不变，错误只提示一次。

## 边界

以下请求不属于 MoAI 后端 API，继续使用原生 `fetch`：

- `/models.json` 本地静态资源。
- 后端返回的文件下载 URL。
- OSS 或兼容对象存储的预签名上传 URL。

生成目录 `src/api/client/` 不做手工修改。页面不直接依赖生成客户端，仍只调用 `src/api/wiki.ts` 封装。

## 实现方案

删除 `src/api/wiki.ts` 中的 `authedFetch`、`postJson`、`Env`、store 和手工错误解析依赖。每个后端操作在封装函数内部获取 `getApiClient()`，按生成 builder 的路径参数、查询参数和请求模型调用对应方法。

封装层继续负责把 Kiota 生成模型适配成当前页面使用的稳定返回类型，包括空值默认值、`int64` 字符串/数字兼容和仅返回 `value` 的响应解包。上传流程仍为“Kiota 预上传 -> 预签名 URL 原生 PUT -> Kiota 完成登记”。

统一错误处理维持单一职责：`FilterRequestHandler` 解析非 2xx 响应、展示反馈并抛出异常；页面只负责结束 loading 或保持表单状态，不重复调用 `feedback.handleError`。若发现页面对同一个 Kiota 异常再次提示，则移除重复提示。

## 验证

- API 层聚焦测试验证 Wiki 封装调用正确的 Kiota builder、参数和请求体。
- 中间件现有测试或新增测试验证业务错误、500、网络错误和 401 的反馈行为。
- 检查代码库中 MoAI 后端请求不再通过手写 `fetch` 发起；只允许本地静态资源、下载 URL 和预签名上传 URL。
- 执行相关 Vitest、TypeScript 类型检查和 ESLint。

## 验收标准

1. Wiki 文档列表、上传登记、删除、重命名、下载地址、内容提取、切割、元数据生成、向量化和模型配置全部走 Kiota。
2. 后端返回业务错误时展示后端 `detail` 或字段错误；500 和网络异常展示统一通知。
3. 401 继续清理登录态并跳转登录页。
4. 同一次失败不产生重复错误提示。
5. 外部下载和预签名上传仍可正常工作。