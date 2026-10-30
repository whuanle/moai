/**
 * 主题预设调色板：每套主题一组核心色，config.ts 据此生成 antd ThemeConfig。
 * 参考Maomi Editor 的多套亮/暗主题：经典为默认，变体只覆盖中性色与强调色，语义色（成功/警告/错误）全主题共用。
 */

export type ThemeMode = 'light' | 'dark'

export type ThemeKey =
  | 'light'
  | 'paper'
  | 'sakura'
  | 'green'
  | 'dark'
  | 'midnight'
  | 'forest'
  | 'violet'

export interface ThemePalette {
  mode: ThemeMode
  /** 强调色（主按钮/链接/选中态） */
  accent: string
  /** 页面底色（body 与 Layout body） */
  bgLayout: string
  /** 卡片与输入类控件容器色 */
  bgContainer: string
  /** 弹层/下拉等浮层底色 */
  bgElevated: string
  textPrimary: string
  textSecondary: string
  textTertiary: string
  textQuaternary: string
  border: string
  borderSecondary: string
  /** 侧边导航 */
  menu: {
    item: string
    hover: string
    hoverBg: string
    selected: string
    selectedBg: string
  }
  /** 表格头 */
  table: {
    headerBg: string
    headerColor: string
    borderColor: string
  }
}

export const themePalettes: Record<ThemeKey, ThemePalette> = {
  // ===== 亮色系 =====
  light: {
    mode: 'light',
    accent: '#6D5AE6',
    bgLayout: '#F7F7FB',
    bgContainer: '#FFFFFF',
    bgElevated: '#FFFFFF',
    textPrimary: '#191B2E',
    textSecondary: '#4B4D66',
    textTertiary: '#8A8BA3',
    textQuaternary: '#B4B5C6',
    border: '#E6E7EF',
    borderSecondary: '#F2F2F7',
    menu: {
      item: '#4B4D66',
      hover: '#191B2E',
      hoverBg: '#F5F4FB',
      selected: '#5743D6',
      selectedBg: '#F0EDFD',
    },
    table: { headerBg: '#FAFAFD', headerColor: '#71738E', borderColor: '#F2F2F7' },
  },
  paper: {
    mode: 'light',
    accent: '#8A5A24',
    bgLayout: '#F5F1E8',
    bgContainer: '#FDFBF6',
    bgElevated: '#FDFBF6',
    textPrimary: '#3D3A33',
    textSecondary: '#6B665A',
    textTertiary: '#948D7C',
    textQuaternary: '#B8B09E',
    border: '#E2DAC8',
    borderSecondary: '#EAE3D3',
    menu: {
      item: '#6B665A',
      hover: '#4F4A3E',
      hoverBg: '#EFE9DB',
      selected: '#8A5A24',
      selectedBg: '#F1E7D4',
    },
    table: { headerBg: '#F5F1E8', headerColor: '#6B665A', borderColor: '#EAE3D3' },
  },
  sakura: {
    mode: 'light',
    accent: '#AB3A64',
    bgLayout: '#FAF1F3',
    bgContainer: '#FDF9FA',
    bgElevated: '#FDF9FA',
    textPrimary: '#403038',
    textSecondary: '#6F5A63',
    textTertiary: '#9C8590',
    textQuaternary: '#C2A9B2',
    border: '#EFD8DE',
    borderSecondary: '#F5E5E9',
    menu: {
      item: '#6F5A63',
      hover: '#513C45',
      hoverBg: '#F5E7EA',
      selected: '#AB3A64',
      selectedBg: '#F8E3EA',
    },
    table: { headerBg: '#FAF1F3', headerColor: '#6F5A63', borderColor: '#F5E5E9' },
  },
  green: {
    mode: 'light',
    accent: '#0E7A5F',
    bgLayout: '#EFF7F2',
    bgContainer: '#FBFEFC',
    bgElevated: '#FBFEFC',
    textPrimary: '#293832',
    textSecondary: '#55695F',
    textTertiary: '#8A9D93',
    textQuaternary: '#B0C2B9',
    border: '#D8E8DF',
    borderSecondary: '#E2EFE8',
    menu: {
      item: '#55695F',
      hover: '#37473F',
      hoverBg: '#E4F0EA',
      selected: '#0E7A5F',
      selectedBg: '#DFF0E7',
    },
    table: { headerBg: '#EFF7F2', headerColor: '#55695F', borderColor: '#E2EFE8' },
  },
  // ===== 暗色系 =====
  dark: {
    mode: 'dark',
    accent: '#2970FF',
    bgLayout: '#101828',
    bgContainer: '#1C2536',
    bgElevated: '#1C2536',
    textPrimary: '#F2F4F7',
    textSecondary: '#C0C5D0',
    textTertiary: '#8A93A5',
    textQuaternary: '#556070',
    border: '#344054',
    borderSecondary: '#2A3446',
    menu: {
      item: '#8A93A5',
      hover: '#C0C5D0',
      hoverBg: '#222D44',
      selected: '#7BA2FF',
      selectedBg: '#24304A',
    },
    table: { headerBg: '#222D44', headerColor: '#C0C5D0', borderColor: '#2A3446' },
  },
  midnight: {
    mode: 'dark',
    accent: '#60A5FA',
    bgLayout: '#0F1419',
    bgContainer: '#131A22',
    bgElevated: '#131A22',
    textPrimary: '#C3CEDB',
    textSecondary: '#8FA0B3',
    textTertiary: '#647485',
    textQuaternary: '#4A5A6B',
    border: '#2B3A49',
    borderSecondary: '#1D2833',
    menu: {
      item: '#8FA0B3',
      hover: '#C3CEDB',
      hoverBg: '#17202B',
      selected: '#60A5FA',
      selectedBg: '#16222E',
    },
    table: { headerBg: '#17202B', headerColor: '#8FA0B3', borderColor: '#1D2833' },
  },
  forest: {
    mode: 'dark',
    accent: '#4ADE80',
    bgLayout: '#101613',
    bgContainer: '#141C17',
    bgElevated: '#141C17',
    textPrimary: '#C2CFC6',
    textSecondary: '#8FA295',
    textTertiary: '#647A6C',
    textQuaternary: '#4A5C50',
    border: '#2C3D33',
    borderSecondary: '#1E2A23',
    menu: {
      item: '#8FA295',
      hover: '#C2CFC6',
      hoverBg: '#18231C',
      selected: '#4ADE80',
      selectedBg: '#1A291F',
    },
    table: { headerBg: '#18231C', headerColor: '#8FA295', borderColor: '#1E2A23' },
  },
  violet: {
    mode: 'dark',
    accent: '#A78BFA',
    bgLayout: '#17131F',
    bgContainer: '#1C1726',
    bgElevated: '#1C1726',
    textPrimary: '#CDC8DB',
    textSecondary: '#9C94B3',
    textTertiary: '#6F6785',
    textQuaternary: '#524A66',
    border: '#362C47',
    borderSecondary: '#271F33',
    menu: {
      item: '#9C94B3',
      hover: '#CDC8DB',
      hoverBg: '#201A2D',
      selected: '#A78BFA',
      selectedBg: '#251E36',
    },
    table: { headerBg: '#201A2D', headerColor: '#9C94B3', borderColor: '#271F33' },
  },
}
