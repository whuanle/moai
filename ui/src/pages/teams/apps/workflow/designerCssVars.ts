import type { CSSProperties } from 'react'
import type { GlobalToken } from 'antd'

/** 把 antd 主题 token 映射为 workflow-designer.css 使用的 --wfd-* CSS 变量（流程设计器整体暗色/亮色主题桥接） */
export function designerCssVars(token: GlobalToken): CSSProperties {
  return {
    '--wfd-bg': token.colorBgContainer,
    '--wfd-bg-subtle': token.colorFillQuaternary,
    '--wfd-border': token.colorBorderSecondary,
    '--wfd-text': token.colorText,
    '--wfd-text-secondary': token.colorTextSecondary,
    '--wfd-text-tertiary': token.colorTextTertiary,
    '--wfd-text-quaternary': token.colorTextQuaternary,
    '--wfd-fill': token.colorFillTertiary,
    '--wfd-fill-hover': token.colorFillSecondary,
    '--wfd-fill-active': token.colorFill,
    '--wfd-primary': token.colorPrimary,
    '--wfd-primary-bg': token.colorPrimaryBg,
    '--wfd-primary-border': token.colorPrimaryBorder,
    '--wfd-success': token.colorSuccess,
    '--wfd-warning': token.colorWarning,
    '--wfd-error': token.colorError,
    '--wfd-error-bg': token.colorErrorBg,
    '--wfd-shadow': token.boxShadow,
    '--wfd-shadow-lg': token.boxShadowSecondary,
    '--wfd-shadow-sm': token.boxShadowTertiary,
  } as CSSProperties
}
