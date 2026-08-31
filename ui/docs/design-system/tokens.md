# 令牌 tokens

对应代码：`ui/src/design-system/theme/tokens.ts`。

## 色彩
| 令牌 | 值 | 用途 |
|---|---|---|
| `colorPrimary` | `#4A9EFF` | 品牌主色 |
| `brandColors.primary` | `#4A9EFF` | 主色别名 |
| `brandColors.success` | `#00B578` | 成功 |
| `brandColors.warning` | `#FF9500` | 警告 |
| `brandColors.error` | `#FF3B30` | 错误 |
| `brandColors.info` | `#4A9EFF` | 信息 |

## 间距（基步 4px，规则值 8/16/24/32/48）
| 令牌 | 值 |
|---|---|
| `spacing.xxs` | 4 |
| `spacing.xs` | 8 |
| `spacing.sm` | 12 |
| `spacing.md` | 16 |
| `spacing.md` | 16 |
| `spacing.lg` | 24 |
| `spacing.xl` | 32 |
| `spacing.xxl` | 48 |

## 圆角 / 字号
- `radius.sm|default|lg` = 4 / 8 / 12。
- `fontSize.xs..xxl` = 12 / 13 / 14 / 16 / 20 / 24。

## 规则
- 页面一律通过 `import { ... } from '@/design-system'` 取用令牌。
- 禁止在 jsx/style 内写魔法数字或硬编码色值。
