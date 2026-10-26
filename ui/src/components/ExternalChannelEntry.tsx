import type { ReactNode } from 'react'
import { Button, Tooltip } from 'antd'
import { useTranslation } from 'react-i18next'
import type { AppExternalChannel } from '@/api/app'
import { DingTalkIcon } from '@/components/DingTalkIcon'
import { FeishuIcon } from '@/components/FeishuIcon'

type ChannelType = NonNullable<AppExternalChannel['channelType']>

/** 各外部渠道的品牌图标与提示文案；跳转链接由后端按平台连接信息生成 */
const CHANNEL_META: Partial<Record<ChannelType, { labelKey: string; icon: ReactNode }>> = {
  feishu: { labelKey: 'appManage.openInFeishu', icon: <FeishuIcon size={16} /> },
  dingtalk: { labelKey: 'appManage.openInDingTalk', icon: <DingTalkIcon size={16} /> },
}

/**
 * 应用卡片右上角的外部渠道入口：已开通的渠道（飞书/钉钉）各显示一个品牌图标，
 * 点击唤起对应客户端并进入机器人单聊（飞书 applink / 钉钉 jumprobot，链接由后端生成）。
 */
export function ExternalChannelEntry({ channels }: { channels?: AppExternalChannel[] | null }) {
  const { t } = useTranslation()
  const entries = (channels ?? []).flatMap((channel) => {
    const meta = channel.channelType ? CHANNEL_META[channel.channelType] : undefined
    if (!meta || !channel.chatUrl) return []
    return [{ key: channel.channelType as ChannelType, meta, chatUrl: channel.chatUrl }]
  })
  if (entries.length === 0) return null

  return (
    <>
      {entries.map(({ key, meta, chatUrl }) => (
        <Tooltip key={key} title={t(meta.labelKey)}>
          <Button
            type="text"
            size="small"
            aria-label={t(meta.labelKey)}
            icon={meta.icon}
            style={{ width: 22, height: 22, minWidth: 0, padding: 0, flexShrink: 0 }}
            onClick={() => window.open(chatUrl, '_blank', 'noopener,noreferrer')}
          />
        </Tooltip>
      ))}
    </>
  )
}
