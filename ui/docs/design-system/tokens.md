# 令牌 tokens

对应代码：`ui/src/design-system/theme/tokens.ts`。

## 色彩
| 令牌 | 值 | 用途 |
|---|---|---|
| `colorPrimary` | `#2970FF` | 品牌主色 |
| `brandColors.primary` | `#2970FF` | 主色别名 |
| `brandColors.success` | `#17B26A` | 成功 |
| `brandColors.warning` | `#F79009` | 警告 |
| `brandColors.error` | `#F04438` | 错误 |
| `brandColors.info` | `#0BA5EC` | 信息 |
| `brandGradient` | `#2A6FFF → #3D8BFF → #68A9FF` | 品牌渐变（强调面，渐变上固定白字） |
| `chartColors` | AntV 八色 | 图表/分类回退色板（共用，勿在页面各存一份） |
| `useNeutralColors()` | antd token 派生 | 主题感知中性色（键与 `neutralColors` 对齐）；页面取中性色一律用它，`neutralColors` 仅浅色一套 |

## 间距（基步 4px，规则值 8/16/24/32/48）
| 令牌 | 值 |
|---|---|
| `spacing.xxs` | 4 |
| `spacing.xs` | 8 |
| `spacing.sm` | 12 |
| `spacing.md` | 16 |
| `spacing.lg` | 24 |
| `spacing.xl` | 32 |
| `spacing.xxl` | 48 |

## 圆角 / 字号
- `radius.sm|default|lg` = 6 / 8 / 12。
- `fontSize.xs..xxl` = 12 / 13 / 14 / 16 / 20 / 24。

## 规则
- 页面一律通过 `import { ... } from '@/design-system'` 取用令牌。
- 禁止在 jsx/style 内写魔法数字或硬编码色值。
