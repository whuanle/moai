import { useEffect, useState } from 'react'
import { Button, Input, Space, Tag, Typography } from 'antd'
import { CopyOutlined } from '@ant-design/icons'
import { useTranslation } from 'react-i18next'
import { Card as DSCard, feedback } from '@/design-system'
import { spacing } from '@/design-system/theme'
import { getServerInfo } from '@/api/auth'

const { Text } = Typography

/** A2A 客户端可调用的 JSON-RPC 方法（message/stream 为 SSE 流式；GET {地址}/agent.json 为 Agent Card） */
const A2A_METHODS = ['message/send', 'message/stream', 'tasks/get', 'tasks/cancel']

/**
 * 应用「A2A」页：展示该应用的 A2A 端点地址（Google Agent2Agent，POST JSON-RPC）与 Agent Card 地址，
 * 供外部 agent 接入；鉴权携带勾选「应用 A2A」范围的应用接入 key。
 * 地址取 serverinfo 的 serviceUrl（对外完整 URL），未取到时回退当前页面 origin。
 */
export function AppA2aSection({ appId }: { appId: string }) {
  const { t } = useTranslation()
  const [address, setAddress] = useState('')

  useEffect(() => {
    let cancelled = false
    const fallback = `${window.location.origin.replace(/\/$/, '')}/api/external/app/${appId}/a2a`
    getServerInfo()
      .then((info) => {
        if (cancelled) return
        const base = info?.serviceUrl?.trim()?.replace(/\/$/, '')
        setAddress(base ? `${base}/api/external/app/${appId}/a2a` : fallback)
      })
      .catch(() => {
        if (cancelled) return
        setAddress(fallback)
      })
    return () => {
      cancelled = true
    }
  }, [appId])

  const copyAddress = async () => {
    try {
      await navigator.clipboard.writeText(address)
      feedback.success(t('common.copySuccess'))
    } catch {
      feedback.error(t('common.copyFailed'))
    }
  }

  return (
    <DSCard>
      <Space direction="vertical" size={spacing.md} style={{ display: 'flex' }}>
        <div>
          <Text strong style={{ fontSize: 13 }}>{t('appWorkspace.a2aAddress')}</Text>
          <Space.Compact style={{ width: '100%', marginTop: spacing.xs }}>
            <Input readOnly value={address} placeholder={t('appWorkspace.a2aAddressPlaceholder')} />
            <Button icon={<CopyOutlined />} disabled={!address} onClick={() => void copyAddress()} aria-label={t('common.copy')} />
          </Space.Compact>
          <Text type="secondary" style={{ fontSize: 12 }}>{address ? `${address}/agent.json` : ''}</Text>
        </div>
        <div>
          <Text strong style={{ fontSize: 13 }}>{t('appWorkspace.a2aMethods')}</Text>
          <div style={{ marginTop: spacing.xs }}>
            {A2A_METHODS.map((name) => (
              <Tag key={name} style={{ fontFamily: 'monospace' }}>{name}</Tag>
            ))}
          </div>
        </div>
        <Text type="secondary" style={{ fontSize: 12 }}>{t('appWorkspace.a2aAuthHint')}</Text>
      </Space>
    </DSCard>
  )
}
