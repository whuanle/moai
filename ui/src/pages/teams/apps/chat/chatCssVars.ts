import type { CSSProperties } from 'react'
import type { GlobalToken } from 'antd'

/** 把 antd 主题 token 映射为 app-chat.css 使用的 --mc-* CSS 变量（AppChat 与调试面板共用） */
export function chatCssVars(token: GlobalToken): CSSProperties {
  return {
    '--mc-primary': token.colorPrimary,
    '--mc-primary-bg': token.colorPrimaryBg,
    '--mc-primary-border': token.colorPrimaryBorder,
    '--mc-info': token.colorInfo,
    '--mc-warning': token.colorWarning,
    '--mc-warning-bg': token.colorWarningBg,
    '--mc-warning-border': token.colorWarningBorder,
    '--mc-success': token.colorSuccess,
    '--mc-bg': token.colorBgContainer,
    '--mc-bg-layout': token.colorBgLayout,
    '--mc-bg-elevated': token.colorBgElevated,
    '--mc-text': token.colorText,
    '--mc-text-secondary': token.colorTextSecondary,
    '--mc-text-tertiary': token.colorTextTertiary,
    '--mc-border': token.colorBorderSecondary,
    '--mc-border-strong': token.colorBorder,
    '--mc-fill': token.colorFillQuaternary,
    '--mc-fill-secondary': token.colorFillTertiary,
    '--mc-code-bg': token.colorFillQuaternary,
    '--mc-radius': `${token.borderRadiusLG}px`,
    '--mc-radius-sm': `${token.borderRadius}px`,
    '--mc-shadow': token.boxShadow,
    '--mc-shadow-lg': token.boxShadowSecondary,
  } as CSSProperties
}
