export const colorPrimary = '#6D5AE6'

export const brandColors = {
  primary: colorPrimary,
  primaryHover: '#5B48D6',
  primaryActive: '#4E3CC9',
  success: '#17B26A',
  warning: '#F79009',
  error: '#F04438',
  info: '#0BA5EC',
}

/** 品牌渐变（Dashboard 欢迎横幅等强调面；渐变上固定用白字，浅/暗主题共用） */
export const brandGradient = 'linear-gradient(135deg, #7C5CFF 0%, #6D5AE6 55%, #4DA3FF 100%)'

/** 图表/分类回退色板（知识图谱等场景共用，避免页面各存一份漂移） */
export const chartColors = ['#5B8FF9', '#5AD8A6', '#F6BD16', '#E8684A', '#6DC8EC', '#9270CA', '#FF9D4D', '#269A99']

export const neutralColors = {
  textPrimary: '#101828',
  textSecondary: '#475467',
  textTertiary: '#667085',
  border: '#E5E7EB',
  background: '#F9FAFB',
  backgroundElevated: '#FFFFFF',
}

export const radius = {
  sm: 6,
  default: 8,
  lg: 12,
  pill: 999,
}

export const spacing = {
  xxs: 4,
  xs: 8,
  sm: 12,
  md: 16,
  lg: 24,
  xl: 32,
  xxl: 48,
}

export const fontFamily =
  "Inter, -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif"

export const fontSize = {
  xs: 12,
  sm: 13,
  md: 14,
  lg: 16,
  xl: 20,
  xxl: 24,
}

export const controlHeight = 36

/**
 * DataTable 卡片化承载（容器底色 + 边框）后的纵向额外占位（上下 1px 边框 ×2）。
 * sticky 表格滚动高度计算（如 Users/AdminSubsystems 等 `calc(100vh - …)`）需计入。
 */
export const tableCardChromeHeight = 2
