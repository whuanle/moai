import { Typography, theme } from 'antd'
import { spacing } from '@/design-system/theme'
import { formatDateTime } from '@/utils/datetime'

export interface AuditCellProps {
  /** 操作人名称，空值时降级为仅时间/占位符 */
  user?: string | null
  /** 时间值（ISO 字符串），经 formatDateTime 格式化 */
  time?: string | null
}

/** 列表审计信息两行单元格：操作人在上、时间次要信息在下，压缩横向占位（表格「创建/更新」列标准形态） */
export function AuditCell({ user, time }: AuditCellProps) {
  const { token } = theme.useToken()

  if (!user) {
    // 后端未返回操作人时降级为单行时间，避免出现无意义的空行
    return (
      <Typography.Text type='secondary' style={{ fontSize: token.fontSizeSM }} ellipsis>
        {formatDateTime(time)}
      </Typography.Text>
    )
  }

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: spacing.xxs, minWidth: 0 }}>
      <Typography.Text ellipsis>{user}</Typography.Text>
      <Typography.Text type='secondary' style={{ fontSize: token.fontSizeSM }} ellipsis>
        {formatDateTime(time)}
      </Typography.Text>
    </div>
  )
}
