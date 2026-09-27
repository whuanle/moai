import { useCallback, useEffect, useMemo, useState } from 'react'
import { Alert, Button, Space, Tag, Typography } from 'antd'
import type { TableColumnsType } from 'antd'
import { CopyOutlined, ThunderboltOutlined } from '@ant-design/icons'
import { useTranslation } from 'react-i18next'
import { Card as DSCard, DataTable } from '@/design-system'
import { spacing } from '@/design-system/theme'
import { Env } from '@/config/env'
import {
  getTeamGatewayModels,
  type TeamGatewayModelItem,
} from '@/api/gateway'

const { Text, Paragraph } = Typography

/** 重置周期单位：0=不重置 1=小时 2=天 3=周 4=月（对齐后端） */
const PERIOD_UNIT_KEYS = ['noReset', 'hour', 'day', 'week', 'month'] as const

/** 将后端 long（可能序列化为字符串）安全转数值 */
function toNumber(v: number | string | null | undefined): number {
  const n = typeof v === 'number' ? v : Number(v)
  return Number.isFinite(n) ? n : 0
}

/**
 * 团队模型网关区块：接入说明 + 可用模型/额度。
 * 接入统一使用应用接入 key（moai-ac-，「应用接入」页维护），本页不再提供密钥管理。
 */
export function TeamGateway({ teamId }: { teamId: number }) {
  const { t } = useTranslation()
  const [models, setModels] = useState<TeamGatewayModelItem[]>([])
  const [modelsLoading, setModelsLoading] = useState(true)
  const [baseUrl, setBaseUrl] = useState('')

  useEffect(() => {
    // 展示后端接入地址：dev 取 VITE_ServerUrl（后端 5000），生产同源部署回退 origin
    setBaseUrl(`${Env.serverUrl}/api/aigateway/${teamId}/v1`)
  }, [teamId])

  const reloadModels = useCallback(async () => {
    if (!Number.isFinite(teamId) || teamId <= 0) return
    setModelsLoading(true)
    try {
      setModels(await getTeamGatewayModels(teamId))
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setModelsLoading(false)
    }
  }, [teamId])

  useEffect(() => {
    void reloadModels()
  }, [reloadModels])

  const copyText = async (text: string) => {
    try {
      await navigator.clipboard.writeText(text)
    } catch {
      // 剪贴板不可用时忽略，地址文本仍可手动复制
    }
  }

  const modelColumns: TableColumnsType<TeamGatewayModelItem> = useMemo(
    () => [
      { title: t('gateway.colModelName'), dataIndex: 'name' },
      {
        title: t('gateway.colModelId'),
        dataIndex: 'modelId',
        render: (v: string | null) => <Text code>{v || '-'}</Text>,
      },
      { title: t('gateway.colChannel'), dataIndex: 'channelName', width: 160 },
      {
        title: t('gateway.colQuota'),
        key: 'quota',
        width: 220,
        render: (_, record) => {
          if (!record.quota) return <Tag color="blue">{t('gateway.quotaUnlimited')}</Tag>
          const used = toNumber(record.quota.usedTokens)
          const limit = toNumber(record.quota.limitValue)
          const unitKey = PERIOD_UNIT_KEYS[record.quota.periodUnit ?? 0] ?? 'noReset'
          return (
            <Space size={spacing.xs} wrap>
              <span>
                {used.toLocaleString()} / {limit.toLocaleString()}
              </span>
              <Text type="secondary" style={{ fontSize: 12 }}>
                {t('gateway.periodPer', { period: t(`gateway.period.${unitKey}`, { count: record.quota.periodValue ?? 1, value: record.quota.periodValue ?? 1 }) })}
              </Text>
            </Space>
          )
        },
      },
      {
        title: t('gateway.colTotalUsed'),
        key: 'totalUsed',
        width: 140,
        render: (_, record) => toNumber(record.totalUsedTokens).toLocaleString(),
      },
    ],
    [t],
  )

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: spacing.lg }}>
      <Alert
        type="info"
        showIcon
        icon={<ThunderboltOutlined />}
        message={t('gateway.endpointTitle')}
        description={
          <div>
            <Paragraph style={{ marginBottom: spacing.xs }}>
              <Text type="secondary">Base URL: </Text>
              <Text code>{baseUrl}</Text>
              <Button type="text" size="small" icon={<CopyOutlined />} aria-label={t('common.copy')} onClick={() => void copyText(baseUrl)} />
            </Paragraph>
            <Text type="secondary" style={{ fontSize: 12 }}>
              {t('gateway.endpointHint')}
            </Text>
          </div>
        }
      />

      <DSCard title={t('gateway.modelsTitle')} styles={{ body: { paddingTop: spacing.sm } }}>
        <DataTable<TeamGatewayModelItem>
          sticky
          rowKey="aiModelId"
          columns={modelColumns}
          dataSource={models}
          loading={modelsLoading}
          onRefresh={() => void reloadModels()}
          refreshLoading={modelsLoading}
          scroll={{ y: 360 }}
        />
      </DSCard>
    </div>
  )
}
