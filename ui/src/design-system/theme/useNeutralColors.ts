import { theme } from 'antd'

/**
 * 主题感知的中性色：字段与 tokens.ts 的 neutralColors 对齐，暗色下随 antd darkAlgorithm 自动翻转。
 * 页面里凡是用到中性色（卡片分隔线/次级文字等）一律用本钩子，不要直接引用只有浅色一套的 neutralColors。
 */
export function useNeutralColors() {
  const { token } = theme.useToken()
  return {
    textPrimary: token.colorText,
    textSecondary: token.colorTextSecondary,
    textTertiary: token.colorTextTertiary,
    border: token.colorBorderSecondary,
    background: token.colorBgLayout,
    backgroundElevated: token.colorBgContainer,
  }
}
