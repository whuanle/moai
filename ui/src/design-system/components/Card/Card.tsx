import { Card as AntdCard, theme } from 'antd'
import type { CardProps } from 'antd'

export type { CardProps }
export function Card(props: CardProps) {
  const { token } = theme.useToken()
  return (
    <AntdCard
      variant="borderless"
      style={{
        border: `1px solid ${token.colorBorderSecondary}`,
        boxShadow: token.boxShadowTertiary,
        ...props.style,
      }}
      styles={{
        body: { padding: 20 },
        header: { borderBottom: `1px solid ${token.colorBorderSecondary}`, fontWeight: 600 },
        ...props.styles,
      }}
      {...props}
    />
  )
}
