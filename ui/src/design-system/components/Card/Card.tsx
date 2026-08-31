import { Card as AntdCard } from 'antd'
import type { CardProps } from 'antd'

export type { CardProps }
export function Card(props: CardProps) {
  return <AntdCard bordered={false} {...props} />
}
