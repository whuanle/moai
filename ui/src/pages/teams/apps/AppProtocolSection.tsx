import { useCallback, useEffect, useState } from 'react'
import { Button, Divider, Input, Space, Switch, Tag, Tooltip, Typography } from 'antd'
import { CopyOutlined } from '@ant-design/icons'
import { useTranslation } from 'react-i18next'
import { Card as DSCard } from '@/design-system'
import { spacing } from '@/design-system/theme'
import { getServerInfo } from '@/api/auth'
import { getAppFeature, updateAppFeature, type AppFeature } from '@/api/access-app'
import { createTeamResourceKey } from '@/api/teamres-key'
import { ResourceKeyCard } from './ResourceKeyCard'

const { Text } = Typography

/** ACP 客户端可调用的 JSON-RPC 方法（session/prompt 为 SSE 流式） */
const ACP_METHODS = ['initialize', 'session/new', 'session/load', 'session/prompt', 'session/cancel']

/** A2A 客户端可调用的 JSON-RPC 方法（message/stream 为 SSE 流式；GET {地址}/agent.json 为 Agent Card） */
const A2A_METHODS = ['message/send', 'message/stream', 'tasks/get', 'tasks/cancel']

interface AppProtocolSectionProps {
  appId: string
  /** 归属团队 id */
  teamId: number
  /** 团队 Admin+ 才能开启协议接入与管理资源 key */
  canManage: boolean
}

/**
 * 应用「ACP/A2A」页：单一开关同开同关两种对外协议接入（开启时后端自动生成/复用该应用唯一的团队资源 key，两种协议共用一把），
 * 分别展示 ACP 端点地址（POST JSON-RPC，agent-to-agent）与 A2A 端点地址（Google Agent2Agent + Agent Card），供外部 agent 复制接入。
 * 地址取 serverinfo 的 serviceUrl（对外完整 URL），未取到时回退当前页面 origin。
 */
export function AppProtocolSection({ appId, teamId, canManage }: AppProtocolSectionProps) {
  const { t } = useTranslation()
  const [acpAddress, setAcpAddress] = useState('')
  const [a2aAddress, setA2aAddress] = useState('')
  const [feature, setFeature] = useState<AppFeature | null>(null)

  const load = useCallback(async () => {
    if (!canManage) return
    try {
      let feat = await getAppFeature(appId)
      // 功能已开启但 key 缺失（历史异常场景）：自动 get-or-create 补齐后重取
      if (feat.isAcpA2aEnabled && !feat.key) {
        await createTeamResourceKey({ teamId, resourceType: 'app', resourceId: appId })
        feat = await getAppFeature(appId)
      }
      setFeature(feat)
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }, [appId, teamId, canManage])

  useEffect(() => {
    if (canManage) void load()
  }, [canManage, load])

  useEffect(() => {
    let cancelled = false
    const fallbackBase = window.location.origin.replace(/\/$/, '')
    getServerInfo()
      .then((info) => {
        if (cancelled) return
        const base = info?.serviceUrl?.trim()?.replace(/\/$/, '') || fallbackBase
        setAcpAddress(`${base}/api/teamres/app/${appId}/acp`)
        setA2aAddress(`${base}/api/teamres/app/${appId}/a2a`)
      })
      .catch(() => {
        if (cancelled) return
        setAcpAddress(`${fallbackBase}/api/teamres/app/${appId}/acp`)
        setA2aAddress(`${fallbackBase}/api/teamres/app/${appId}/a2a`)
      })
    return () => {
      cancelled = true
    }
  }, [appId])

  const handleToggle = async (checked: boolean) => {
    try {
      setFeature(await updateAppFeature(appId, checked))
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }

  const copyAddress = async (value: string) => {
    try {
      await navigator.clipboard.writeText(value)
    } catch {
      // 剪贴板失败静默（地址只读可见可手选）
    }
  }

  const protocolOn = feature?.isAcpA2aEnabled ?? false

  return (
    <DSCard>
      <Space direction="vertical" size={spacing.md} style={{ display: 'flex', maxWidth: 880 }}>
        {/* 功能开关行：ACP/A2A 同开同关 */}
        <Space size={spacing.sm} align="center">
          <Switch checked={protocolOn} onChange={(v) => void handleToggle(v)} />
          <Text strong>{t('appWorkspace.protocolEnable')}</Text>
          <Tag color={protocolOn ? 'success' : 'default'} style={{ marginRight: 0 }}>
            {protocolOn ? t('teamresKey.stateOn') : t('teamresKey.stateOff')}
          </Tag>
        </Space>

        <Divider style={{ margin: 0 }} />

        {/* ACP 端点 */}
        <div>
          <Text strong style={{ fontSize: 13 }}>ACP</Text>
          <Space.Compact style={{ width: '100%', marginTop: spacing.xs }}>
            <Input readOnly value={acpAddress} placeholder={t('appWorkspace.acpAddressPlaceholder')} />
            <Tooltip title={t('common.copy')}>
              <Button icon={<CopyOutlined />} disabled={!acpAddress} onClick={() => void copyAddress(acpAddress)} aria-label={t('common.copy')} />
            </Tooltip>
          </Space.Compact>
          <div style={{ marginTop: spacing.sm, display: 'flex', flexWrap: 'wrap', gap: spacing.xs, alignItems: 'center' }}>
            <Text type="secondary" style={{ fontSize: 12 }}>{t('appWorkspace.acpMethods')}</Text>
            {ACP_METHODS.map((name) => (
              <Tag key={name} style={{ fontFamily: 'monospace', marginRight: 0 }}>{name}</Tag>
            ))}
          </div>
        </div>

        <Divider style={{ margin: 0 }} />

        {/* A2A 端点 */}
        <div>
          <Text strong style={{ fontSize: 13 }}>A2A</Text>
          <Space.Compact style={{ width: '100%', marginTop: spacing.xs }}>
            <Input readOnly value={a2aAddress} placeholder={t('appWorkspace.a2aAddressPlaceholder')} />
            <Tooltip title={t('common.copy')}>
              <Button icon={<CopyOutlined />} disabled={!a2aAddress} onClick={() => void copyAddress(a2aAddress)} aria-label={t('common.copy')} />
            </Tooltip>
          </Space.Compact>
          <div style={{ marginTop: spacing.sm }}>
            <Text type="secondary" style={{ fontSize: 12 }}>{a2aAddress ? `${t('appWorkspace.a2aAgentCard')}：${a2aAddress}/agent.json` : ''}</Text>
          </div>
          <div style={{ marginTop: spacing.sm, display: 'flex', flexWrap: 'wrap', gap: spacing.xs, alignItems: 'center' }}>
            <Text type="secondary" style={{ fontSize: 12 }}>{t('appWorkspace.a2aMethods')}</Text>
            {A2A_METHODS.map((name) => (
              <Tag key={name} style={{ fontFamily: 'monospace', marginRight: 0 }}>{name}</Tag>
            ))}
          </div>
        </div>

        <Divider style={{ margin: 0 }} />

        {/* 资源 Key 盒子（ACP/A2A 共用一把） */}
        <ResourceKeyCard
          featureEnabled={protocolOn}
          canManage={canManage}
          keyInfo={feature ? { teamResourceKeyId: feature.teamResourceKeyId, key: feature.key, keyPrefix: feature.keyPrefix } : null}
          onChanged={() => void load()}
        />
      </Space>
    </DSCard>
  )
}
