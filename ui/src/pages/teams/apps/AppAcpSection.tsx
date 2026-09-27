import { useEffect, useState } from 'react'
import { Button, Input, Space, Tag, Typography } from 'antd'
import { CopyOutlined } from '@ant-design/icons'
import { useTranslation } from 'react-i18next'
import { Card as DSCard, feedback } from '@/design-system'
import { spacing } from '@/design-system/theme'
import { getServerInfo } from '@/api/auth'

const { Text } = Typography

/** ACP 客户端可调用的 JSON-RPC 方法（session/prompt 为 SSE 流式） */
const ACP_METHODS = ['initialize', 'session/new', 'session/load', 'session/prompt', 'session/cancel']

/**
 * 应用「ACP」页：展示该应用的 ACP 端点地址（POST JSON-RPC，agent-to-agent），
 * 供外部 agent 接入；鉴权携带勾选「应用 ACP」范围的应用接入 key。
 * 地址取 serverinfo 的 serviceUrl（对外完整 URL），未取到时回退当前页面 origin。
 */
export function AppAcpSection({ appId }: { appId: string }) {
  const { t } = useTranslation()
  const [address, setAddress] = useState('')

  useEffect(() => {
    let cancelled = false
    const fallback = `${window.location.origin.replace(/\/$/, '')}/api/external/app/${appId}/acp`
    getServerInfo()
      .then((info) => {
        if (cancelled) return
        const base = info?.serviceUrl?.trim()?.replace(/\/$/, '')
        setAddress(base ? `${base}/api/external/app/${appId}/acp` : fallback)
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
          <Text strong style={{ fontSize: 13 }}>{t('appWorkspace.acpAddress')}</Text>
          <Space.Compact style={{ width: '100%', marginTop: spacing.xs }}>
            <Input readOnly value={address} placeholder={t('appWorkspace.acpAddressPlaceholder')} />
            <Button icon={<CopyOutlined />} disabled={!address} onClick={() => void copyAddress()} aria-label={t('common.copy')} />
          </Space.Compact>
        </div>
        <div>
          <Text strong style={{ fontSize: 13 }}>{t('appWorkspace.acpMethods')}</Text>
          <div style={{ marginTop: spacing.xs }}>
            {ACP_METHODS.map((name) => (
              <Tag key={name} style={{ fontFamily: 'monospace' }}>{name}</Tag>
            ))}
          </div>
        </div>
        <Text type="secondary" style={{ fontSize: 12 }}>{t('appWorkspace.acpAuthHint')}</Text>
      </Space>
    </DSCard>
  )
}
