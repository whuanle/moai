import { useEffect, useState } from 'react'
import { Alert, Modal, Typography } from 'antd'
import type { TableColumnsType } from 'antd'
import { useTranslation } from 'react-i18next'
import type { CustomPlugin, CustomPluginFunction } from '@/api/plugin'
import { customPluginApi } from '@/api/plugin'
import { DataTable } from '@/design-system'

const { Text } = Typography

interface FunctionListModalProps {
  open: boolean
  plugin: CustomPlugin | null
  onCancel: () => void
}

export function FunctionListModal({ open, plugin, onCancel }: FunctionListModalProps) {
  const { t } = useTranslation()
  const [functions, setFunctions] = useState<CustomPluginFunction[]>([])
  const [loading, setLoading] = useState(false)

  useEffect(() => {
    if (!open || !plugin?.pluginId) return
    setLoading(true)
    customPluginApi
      .getCustomPluginFunctions(plugin.pluginId)
      .then((items) => setFunctions(items))
      .catch(() => setFunctions([]))
      .finally(() => setLoading(false))
  }, [open, plugin?.pluginId])

  const handleCancel = () => {
    setFunctions([])
    onCancel()
  }

  const columns: TableColumnsType<CustomPluginFunction> = [
    {
      title: t('plugins.colFunctionName'),
      dataIndex: 'name',
      key: 'name',
      width: 200,
      render: (v: string | null) => <Text strong>{v || '-'}</Text>,
    },
    {
      title: t('plugins.colFunctionPath'),
      dataIndex: 'path',
      key: 'path',
      width: 300,
      render: (v: string | null) => (
        <Text type="secondary" style={{ fontFamily: 'monospace', fontSize: 12 }}>
          {v || '-'}
        </Text>
      ),
    },
    {
      title: t('plugins.colFunctionSummary'),
      dataIndex: 'summary',
      key: 'summary',
      width: 'auto',
      render: (v: string | null) => <Text type="secondary">{v || '-'}</Text>,
    },
  ]

  return (
    <Modal
      title={`${plugin?.pluginName ?? ''} - ${t('plugins.functionListTitle')}`}
      open={open}
      onCancel={handleCancel}
      width={1080}
      footer={null}
      maskClosable={false}
      destroyOnClose
      styles={{ body: { height: '60vh', overflow: 'hidden' } }}
    >
      {plugin?.type === 'mcp' && (
        <Alert message={t('plugins.functionListHint')} type="info" showIcon style={{ marginBottom: 16 }} />
      )}
      <DataTable<CustomPluginFunction>
        rowKey="functionId"
        columns={columns}
        dataSource={functions}
        loading={loading}
        pagination={false}
        scroll={{ x: 600, y: 'calc(60vh - 130px)' }}
        onRefresh={() => {
          if (plugin?.pluginId) {
            setLoading(true)
            customPluginApi
              .getCustomPluginFunctions(plugin.pluginId)
              .then((items) => setFunctions(items))
              .finally(() => setLoading(false))
          }
        }}
        refreshLoading={loading}
      />
    </Modal>
  )
}
