import { Card as AntdCard } from 'antd'
import type { CardProps } from 'antd'

export type { CardProps }
export function Card(props: CardProps) {
  return (
    <AntdCard
      variant="borderless"
      style={{
        border: '1px solid rgba(16, 24, 40, 0.08)',
        boxShadow: '0 1px 2px rgba(16, 24, 40, 0.05)',
        ...props.style,
      }}
      styles={{
        body: { padding: 20 },
        header: { borderBottom: '1px solid rgba(16, 24, 40, 0.08)', fontWeight: 600 },
        ...props.styles,
      }}
      {...props}
    />
  )
}
