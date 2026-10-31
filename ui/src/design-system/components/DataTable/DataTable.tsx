import { Button, ConfigProvider, Table, theme } from 'antd'
import type { TableColumnsType, TableProps } from 'antd'
import type { ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { spacing } from '@/design-system/theme'

export interface DataTableProps<RecordType extends object>
  extends Omit<TableProps<RecordType>, 'pagination' | 'columns' | 'dataSource'> {
  columns: TableColumnsType<RecordType>
  dataSource: TableProps<RecordType>['dataSource']
  pagination?: TableProps<RecordType>['pagination']
  toolbar?: ReactNode
  onRefresh?: () => void
  refreshLoading?: boolean
  rowKey?: TableProps<RecordType>['rowKey']
}

export function DataTable<RecordType extends object>({
  toolbar,
  onRefresh,
  refreshLoading,
  pagination,
  rowKey = 'id' as TableProps<RecordType>['rowKey'],
  ...rest
}: DataTableProps<RecordType>) {
  const { t } = useTranslation()
  const { token } = theme.useToken()
  const showTotal = (total: number) => t('ds.table.total', { total })
  const resolvedPagination =
    pagination === false
      ? false
      : ({ showSizeChanger: true, showTotal, ...pagination } as TableProps<RecordType>['pagination'])

  return (
    // 容器底色卡片（与 Card 同语言）：灰表头/白表体/分页整体承载在卡片内，
    // 与上方裸工具行（页面底色）拉开层级；overflow hidden 让表头随卡片圆角裁切。
    // 纵向额外占位 = tableCardChromeHeight（上下边框），sticky 表格滚动高度计算需计入。
    <div
      style={{
        background: token.colorBgContainer,
        border: `1px solid ${token.colorBorderSecondary}`,
        borderRadius: token.borderRadiusLG,
        boxShadow: token.boxShadowTertiary,
        overflow: 'hidden',
      }}
    >
      {(toolbar || onRefresh) && (
        <div
          style={{
            display: 'flex',
            alignItems: 'center',
            gap: spacing.md,
            flexWrap: 'wrap',
            margin: spacing.md,
          }}
        >
          {toolbar}
          {onRefresh && (
            <ConfigProvider button={{ autoInsertSpace: false }}>
              <Button onClick={onRefresh} loading={refreshLoading}>
                {t('ds.table.refresh')}
              </Button>
            </ConfigProvider>
          )}
        </div>
      )}
      <Table<RecordType> rowKey={rowKey} pagination={resolvedPagination} {...rest} />
    </div>
  )
}
