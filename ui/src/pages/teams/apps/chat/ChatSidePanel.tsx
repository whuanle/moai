import {
  BarChartOutlined,
  CloseOutlined,
  CodeOutlined,
  CopyOutlined,
  FileTextOutlined,
} from '@ant-design/icons'
import { Button, Tooltip } from 'antd'
import { useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import ReactMarkdown from 'react-markdown'
import remarkGfm from 'remark-gfm'
import { feedback } from '@/design-system'
import { EChart } from './EChart'
import type { UiPanelItem } from './uiTools'

export interface ChatSidePanelProps {
  /** 已打开的侧边栏内容项（标签页） */
  items: UiPanelItem[]
  /** 当前展示项的 key */
  activeKey: string | null
  /** 切换标签 */
  onSelect: (key: string) => void
  /** 关闭单个标签（关闭的是当前标签时自动切到相邻标签） */
  onCloseTab: (key: string) => void
  /** 收起整个面板（标签保留，点击消息卡片可再展开） */
  onClose: () => void
}

/** 头部标签图标 */
const KIND_ICON = {
  document: FileTextOutlined,
  code: CodeOutlined,
  chart: BarChartOutlined,
} as const

/** 宽度持久化与拖拽边界：主对话区至少保留 480px */
const PANEL_WIDTH_STORAGE_KEY = 'moai:chat:panel-width'
const PANEL_MIN_WIDTH = 360
const PANEL_MAX_WIDTH = 1000
const MAIN_MIN_WIDTH = 480

function loadStoredWidth(): number | null {
  try {
    const raw = localStorage.getItem(PANEL_WIDTH_STORAGE_KEY)
    const value = raw == null ? Number.NaN : Number(raw)
    return Number.isFinite(value) && value >= PANEL_MIN_WIDTH && value <= PANEL_MAX_WIDTH ? value : null
  } catch {
    return null
  }
}

/**
 * 对话右侧侧边栏：承载前端展示工具（ui_ 前缀）产出的内容——markdown 文档 / 代码 / ECharts 图表。
 * 作为 .moai-chat 横向 flex 的第三列停靠（非浮层），主对话区自适应收窄；
 * 头部为标签栏（多份产出可切换、可单个关闭），左缘手柄可拖拽调节宽度（持久化到本地）.
 */
export function ChatSidePanel({ items, activeKey, onSelect, onCloseTab, onClose }: ChatSidePanelProps) {
  const { t } = useTranslation()
  const [width, setWidth] = useState<number | null>(loadStoredWidth)
  const panelRef = useRef<HTMLElement | null>(null)
  const draggingRef = useRef(false)

  if (items.length === 0) return null

  const active = items.find((x) => x.key === activeKey) ?? items[items.length - 1]
  const copyText =
    active.kind === 'document' ? active.content : active.kind === 'code' ? active.code : undefined

  const copy = async () => {
    if (!copyText) return
    try {
      await navigator.clipboard.writeText(copyText)
      feedback.success(t('appChat.copied'))
    } catch {
      // 忽略剪贴板不可用
    }
  }

  // 左缘拖拽调宽：指针捕获跟随，宽度 = 容器右缘 - 指针 x（面板右停靠），松手持久化
  const startResize = (e: React.PointerEvent<HTMLDivElement>) => {
    e.preventDefault()
    draggingRef.current = true
    e.currentTarget.setPointerCapture(e.pointerId)
  }
  const moveResize = (e: React.PointerEvent<HTMLDivElement>) => {
    if (!draggingRef.current) return
    const container = panelRef.current?.parentElement?.getBoundingClientRect()
    if (!container) return
    const max = Math.max(PANEL_MIN_WIDTH, Math.min(PANEL_MAX_WIDTH, container.width - MAIN_MIN_WIDTH))
    setWidth(Math.min(Math.max(container.right - e.clientX, PANEL_MIN_WIDTH), max))
  }
  const endResize = (e: React.PointerEvent<HTMLDivElement>) => {
    if (!draggingRef.current) return
    draggingRef.current = false
    e.currentTarget.releasePointerCapture?.(e.pointerId)
    try {
      setWidth((w) => {
        if (w != null) localStorage.setItem(PANEL_WIDTH_STORAGE_KEY, String(Math.round(w)))
        return w
      })
    } catch {
      // 本地存储不可用时仅本次生效
    }
  }

  return (
    <aside
      ref={panelRef}
      className="moai-chat__panel"
      style={width != null ? ({ '--mc-panel-w': `${Math.round(width)}px` } as React.CSSProperties) : undefined}
      aria-label={t('appChat.uiPanelTitle')}
    >
      <div
        className="moai-chat__panel-resize"
        role="separator"
        aria-label={t('appChat.uiPanelResize')}
        onPointerDown={startResize}
        onPointerMove={moveResize}
        onPointerUp={endResize}
        onPointerCancel={endResize}
      />
      <header className="moai-chat__panel-head">
        <div className="moai-chat__panel-tabs" role="tablist">
          {items.map((item) => {
            const Icon = KIND_ICON[item.kind]
            const kindLabel = t(`appChat.uiToolKind.${item.kind}`)
            const title = item.title || kindLabel
            return (
              <div
                key={item.key}
                role="tab"
                aria-selected={item.key === active.key}
                className={`moai-chat__panel-tab${item.key === active.key ? ' is-active' : ''}`}
                onClick={() => onSelect(item.key)}
                title={title}
              >
                <Icon className="moai-chat__panel-tab-icon" />
                <span className="moai-chat__panel-tab-title">{title}</span>
                <button
                  type="button"
                  className="moai-chat__panel-tab-close"
                  aria-label={t('appChat.uiTabClose')}
                  onClick={(e) => {
                    e.stopPropagation()
                    onCloseTab(item.key)
                  }}
                >
                  <CloseOutlined />
                </button>
              </div>
            )
          })}
        </div>
        <div className="moai-chat__panel-actions">
          {copyText && (
            <Tooltip title={t('appChat.uiPanelCopy')}>
              <Button
                type="text"
                size="small"
                icon={<CopyOutlined />}
                aria-label={t('appChat.uiPanelCopy')}
                onClick={() => void copy()}
              />
            </Tooltip>
          )}
          <Tooltip title={t('appChat.uiPanelClose')}>
            <Button
              type="text"
              size="small"
              icon={<CloseOutlined />}
              aria-label={t('appChat.uiPanelClose')}
              onClick={onClose}
            />
          </Tooltip>
        </div>
      </header>
      <div className="moai-chat__panel-body">
        {active.kind === 'document' && (
          <div className="moai-chat__markdown moai-chat__panel-doc">
            <ReactMarkdown remarkPlugins={[remarkGfm]}>{active.content ?? ''}</ReactMarkdown>
          </div>
        )}
        {active.kind === 'code' && (
          <div className="moai-chat__panel-code-wrap">
            {active.language && <span className="moai-chat__panel-code-lang">{active.language}</span>}
            <pre className="moai-chat__panel-code">{active.code ?? ''}</pre>
          </div>
        )}
        {active.kind === 'chart' && active.option && <EChart option={active.option} />}
      </div>
    </aside>
  )
}
