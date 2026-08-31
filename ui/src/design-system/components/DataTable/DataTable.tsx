import { Button, ConfigProvider, Table } from 'antd'
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
  const showTotal = (total: number) => t('ds.table.total', { total })
  const resolvedPagination =
    pagination === false
      ? false
      : ({ showSizeChanger: true, showTotal, ...pagination } as TableProps<RecordType>['pagination'])

  return (
    <div>
      {(toolbar || onRefresh) && (
        <div
          style={{
            display: 'flex',
            justifyContent: 'space-between',
            alignItems: 'center',
            marginBottom: spacing.md,
          }}
        >
          <div>{toolbar}</div>
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
