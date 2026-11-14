import { ArrowLeftOutlined } from '@ant-design/icons'
import { theme } from 'antd'
import type { ReactNode } from 'react'
import { Link } from 'react-router'

export interface BackLinkProps {
  /** 返回目标路由（上级列表页） */
  to: string
  children: ReactNode
}

/** 页面左上角返回链接：左侧箭头图标 +「返回xx」文案，替代「团队 / 提示词 / 编辑」式多级面包屑 */
export function BackLink({ to, children }: BackLinkProps) {
  const { token } = theme.useToken()
  return (
    <Link
      to={to}
      style={{ display: 'inline-flex', alignItems: 'center', gap: 4, color: token.colorText }}
    >
      <ArrowLeftOutlined aria-hidden />
      {children}
    </Link>
  )
}
