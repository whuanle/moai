# I18nScanner — BusinessException 多语言资源扫描工具

扫描 `src/` 下所有 `new BusinessException(...)` 的消息字符串，生成 Maomi.I18n 可加载的扁平 key/value JSON 资源（`zh-CN.json`、`en-US.json`），供 `src/MoAI/i18n/` 使用。

## 用法

```bash
# 仓库根目录执行（默认扫 <cwd>/src，输出 <cwd>/src/MoAI/i18n）
dotnet run --project tool/I18nScanner

# 常用参数
dotnet run --project tool/I18nScanner -- --dry-run                 # 只看结果不写文件
dotnet run --project tool/I18nScanner -- --report tmp/i18n-report.md  # 附带明细报告
dotnet run --project tool/I18nScanner -- --src <目录> --out <目录>
```

建议时机：新增/修改过 BusinessException 消息后跑一次；en-US.json 里已人工翻译的条目会被保留。

## 提取规则

| 抛出形态 | 生成 key | 说明 |
|---|---|---|
| `new BusinessException("消息.")` | `消息.` | 字面量即 key，运行时精确命中 |
| `new BusinessException(404, "消息.")` | `消息.` | 状态码参数跳过 |
| `new BusinessException("最多 {0} 个.", count)` | `最多 {0} 个.` | 格式串 + Argments，翻译后自动格式化 |
| `new BusinessException($"第 {i} 段不合法.")` | `第 {0} 段不合法.` | ⚠ 归一化模板，运行时 Message 已填充参数，**按 key 不会命中**；需把抛出点改写为格式串 + Argments 才能翻译 |
| 变量/方法调用等表达式 | 不生成 | 打到「未解析」清单人工确认（运行时回退原文，行为安全） |

字符串拼接（`"a" + b`）与跨行拼接可折叠，但只要有一段不可静态解析就整体进「未解析」清单。

## 合并语义

- 已有翻译（value ≠ key 的条目）一律保留；
- 新 key 以原文兜底写入（不翻译也不影响接口返回，IStringLocalizer 缺 key 时原样返回 key）；
- 代码中不再出现的 key 剪除（翻译可在 git 历史找回），剪除清单见报告；
- `zh-CN.json` 以原文兜底生成，作为全部 key 的权威清单；
- JSON 键序按 Ordinal 排序，UTF-8 无 BOM、末尾带换行，保证 diff 干净。

## 运行时链路

`MaomiExceptionFilter` / `MaomiExceptionHandler` 拦截 BusinessException，把消息原文作为 key 调 `IStringLocalizer`，按请求 `Accept-Language` 返回对应语言；中间件管线在 `Program.cs` 挂 `app.UseI18n()`，资源目录在 `MainModule` 里注册（`AddI18nResource` + `AddJsonDirectory`）。

注意：语言头需带完整文化名（`en-US`），裸 `en` 不会匹配 `en-US.json`。
