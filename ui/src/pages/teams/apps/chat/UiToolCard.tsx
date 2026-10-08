import {
  BarChartOutlined,
  CodeOutlined,
  FileTextOutlined,
  LoadingOutlined,
  RightOutlined,
} from '@ant-design/icons'
import { useMemo } from 'react'
import { useTranslation } from 'react-i18next'
import type { ToolCallDisplay } from './ToolCallCard'
import { hasRenderableContent, parseUiToolPayload, type UiToolKind } from './uiTools'

const KIND_ICON: Record<UiToolKind, typeof FileTextOutlined> = {
  document: FileTextOutlined,
  code: CodeOutlined,
  chart: BarChartOutlined,
}

export interface UiToolCardProps {
  toolCall: ToolCallDisplay
  /** 面板当前正展示该项（高亮） */
  active?: boolean
  /** 点击打开侧边栏 */
  onOpen?: (toolCall: ToolCallDisplay) => void
}

/**
 * 前端展示工具折叠卡：ui_ 前缀工具调用在消息流中收缩为一行卡片（类型图标 + 标题 + 查看），
 * 内容不在正文展开；点击在右侧侧边栏打开完整渲染（文档/代码/图表）.
 */
export function UiToolCard({ toolCall, active, onOpen }: UiToolCardProps) {
  const { t } = useTranslation()
  const payload = useMemo(() => parseUiToolPayload(toolCall.name, toolCall.argsJson), [toolCall])
  const renderable = hasRenderableContent(payload)
  const running = toolCall.status === 'running' || toolCall.status === 'approved'
  const Icon = payload ? KIND_ICON[payload.kind] : FileTextOutlined
  const kindLabel = payload ? t(`appChat.uiToolKind.${payload.kind}`) : t('appChat.uiToolKind.document')
  const title = payload?.title || kindLabel

  return (
    <button
      type="button"
      className={`moai-chat__ui-tool${active ? ' is-active' : ''}${renderable ? '' : ' is-disabled'}`}
      disabled={!renderable}
      onClick={() => onOpen?.(toolCall)}
    >
      <span className="moai-chat__ui-tool-icon">
        <Icon />
      </span>
      <span className="moai-chat__ui-tool-kind">{kindLabel}</span>
      <span className="moai-chat__ui-tool-title" title={title}>
        {running ? t('appChat.uiToolGenerating') : title}
      </span>
      <span className="moai-chat__ui-tool-action">
        {running ? <LoadingOutlined spin /> : renderable ? t('appChat.uiToolOpen') : t('appChat.uiToolInvalid')}
        {!running && renderable && <RightOutlined />}
      </span>
    </button>
  )
}
