import { useRef, type ReactNode, type RefObject } from 'react'
import {
  ArrowUpOutlined,
  CloseOutlined,
  LoadingOutlined,
  PaperClipOutlined,
  SafetyCertificateOutlined,
  StopOutlined,
  ThunderboltFilled,
} from '@ant-design/icons'
import { Avatar, Input, Select, Tooltip } from 'antd'
import type { TextAreaRef } from 'antd/es/input/TextArea'
import { useTranslation } from 'react-i18next'
import type { ToolApprovalMode } from '@/api/agentChat'
import type { TeamAvailableModelItem } from '@/api/aichannel'
import type { AppUserTeamAgentOption } from '@/api/app'
import { resolveStorageUrl } from '@/utils/storage'
import { formatAttachmentSize, getAttachmentFileIcon, hasPendingAttachment, MAX_ATTACHMENTS, type ChatAttachmentDraft } from './attachment'

export interface ChatComposerProps {
  value: string
  onValueChange: (value: string) => void
  onSubmit: () => void
  sending: boolean
  onStop: () => void
  placeholder: string
  /** 供外部聚焦输入框（新建会话等场景） */
  inputRef?: RefObject<TextAreaRef | null>
  /** 附件草稿与回调（一套齐传才启用附件入口）：chip 展示/移除由组件渲染 */
  attachments?: ChatAttachmentDraft[]
  onPickFiles?: (files: FileList | null) => void
  onRemoveAttachment?: (id: string) => void
  /** 工具审批模式（auto/approval）与切换回调：不传即隐藏切换 */
  approvalMode?: ToolApprovalMode
  onToggleApprovalMode?: () => void
  /** 对话模型切换器：不传或无候选即隐藏 */
  modelOptions?: TeamAvailableModelItem[]
  selectedModelId?: string | null
  onModelChange?: (modelId: string) => void
  /** 团队智能体切换器（多选 ≥2 时传入）：单选由后端直接生效、无切换器 */
  teamAgentOptions?: AppUserTeamAgentOption[]
  selectedTeamAgentId?: string | null
  onTeamAgentChange?: (teamAgentId: string) => void
  /** 底部右侧扩展（上下文用量环形指示等）：调试面板无数据不传 */
  contextUsageNode?: ReactNode
}

/**
 * 对话输入卡（欢迎态居中 / 对话态停靠底部共用）：无边框多行文本域 + 附件 chips +
 * 底部左侧附件/审批模式/模型切换器、右侧上下文用量与发送/停止。
 * 供正式对话页（AppChat）与配置页调试面板（AppDebugChat）复用，样式依赖全局 app-chat.css；
 * 附件上传/提取状态机见 useChatAttachments，组件只负责展示与回传选择结果。
 */
export function ChatComposer({
  value,
  onValueChange,
  onSubmit,
  sending,
  onStop,
  placeholder,
  inputRef,
  attachments,
  onPickFiles,
  onRemoveAttachment,
  approvalMode,
  onToggleApprovalMode,
  modelOptions,
  selectedModelId,
  onModelChange,
  teamAgentOptions,
  selectedTeamAgentId,
  onTeamAgentChange,
  contextUsageNode,
}: ChatComposerProps) {
  const { t } = useTranslation()
  const fileInputRef = useRef<HTMLInputElement | null>(null)

  const hasAttachments = (attachments?.length ?? 0) > 0
  // 附件中是否有未就绪项：发送按钮置灰并提示（上传/提取中等待，失败项需移除）
  const attachmentsBlocking = hasAttachments && (hasPendingAttachment(attachments!) || attachments!.some((a) => a.status === 'failed'))
  const sendDisabled = (!value.trim() && !hasAttachments) || attachmentsBlocking

  return (
    <div className="moai-chat__composer">
      <Input.TextArea
        ref={inputRef}
        variant="borderless"
        autoSize={{ minRows: 3, maxRows: 10 }}
        value={value}
        onChange={(e) => onValueChange(e.target.value)}
        onPressEnter={(e) => {
          if (!e.shiftKey) {
            e.preventDefault()
            onSubmit()
          }
        }}
        placeholder={placeholder}
      />
      {hasAttachments && (
        <div className="moai-chat__attachments">
          {attachments!.map((att) => {
            // 图片附件就绪后展示缩略图（上传中/失败无地址时回退图片图标）；文档按扩展名取类型图标
            const FileIcon = getAttachmentFileIcon(att.name)
            return (
              <div key={att.id} className={`moai-chat__attachment${att.status === 'failed' ? ' is-failed' : ''}`}>
                {att.isImage && att.url ? (
                  <img src={att.url} alt="" className="moai-chat__attachment-thumb" />
                ) : (
                  <span className="moai-chat__attachment-icon">
                    <FileIcon />
                  </span>
                )}
                <span className="moai-chat__attachment-name" title={att.name}>
                  {att.name}
                </span>
                <span className="moai-chat__attachment-meta">
                  {att.status === 'uploading' || att.status === 'extracting' ? (
                    <>
                      <LoadingOutlined spin />
                      <span>{t(att.status === 'uploading' ? 'appChat.attachmentUploading' : 'appChat.attachmentExtracting')}</span>
                    </>
                  ) : att.status === 'failed' ? (
                    <span>{t('appChat.attachmentFailed')}</span>
                  ) : (
                    <>
                      <span>{formatAttachmentSize(att.size)}</span>
                      {att.truncated && <span>{t('appChat.attachmentTruncated')}</span>}
                    </>
                  )}
                </span>
                <button
                  type="button"
                  className="moai-chat__attachment-remove"
                  onClick={() => onRemoveAttachment?.(att.id)}
                  aria-label={t('appChat.attachmentRemove')}
                >
                  <CloseOutlined />
                </button>
              </div>
            )
          })}
        </div>
      )}
      <div className="moai-chat__composer-footer">
        <div className="moai-chat__composer-tools">
          {onPickFiles && (
            <>
              <input
                ref={fileInputRef}
                type="file"
                multiple
                className="moai-chat__file-input"
                accept=".jpg,.jpeg,.png,.gif,.bmp,.webp,.svg,.md,.pdf,.doc,.docx,.xls,.xlsx,.ppt,.pptx,.txt,.rtf,.odt,.ods,.odp,.csv,.json,.xml,.html,.htm,.epub,.mobi,.eml,.msg,.tex"
                onChange={(e) => {
                  onPickFiles(e.target.files)
                  e.target.value = ''
                }}
              />
              <Tooltip title={t('appChat.attach')}>
                <button
                  type="button"
                  className="moai-chat__attach"
                  disabled={sending || (attachments?.length ?? 0) >= MAX_ATTACHMENTS}
                  onClick={() => fileInputRef.current?.click()}
                  aria-label={t('appChat.attach')}
                >
                  <PaperClipOutlined />
                </button>
              </Tooltip>
            </>
          )}
          {approvalMode && onToggleApprovalMode && (
            <Tooltip title={approvalMode === 'approval' ? t('appChat.modeApprovalHint') : t('appChat.modeAutoHint')}>
              <button
                type="button"
                className={`moai-chat__mode${approvalMode === 'approval' ? ' is-approval' : ''}`}
                onClick={onToggleApprovalMode}
              >
                {approvalMode === 'approval' ? <SafetyCertificateOutlined /> : <ThunderboltFilled />}
                <span>{approvalMode === 'approval' ? t('appChat.modeApproval') : t('appChat.modeAuto')}</span>
              </button>
            </Tooltip>
          )}
          {modelOptions && modelOptions.length > 0 && (
            <Tooltip title={t('appChat.chatModel')}>
              <Select
                size="small"
                variant="borderless"
                showSearch
                style={{ minWidth: 130, maxWidth: 220 }}
                value={selectedModelId ?? undefined}
                onChange={(v) => onModelChange?.(v)}
                options={modelOptions.map((m) => {
                  const name = m.name || m.modelId || '-'
                  const channel = m.channelName || ''
                  // 关闭态与下拉行均显示纯模型名，下拉行内附渠道名（不同渠道可能存在同名模型）
                  const triggerLabel = name
                  return {
                    value: String(m.aiModelId),
                    triggerLabel,
                    label: (
                      <div style={{ display: 'flex', alignItems: 'baseline', gap: 8, minWidth: 0 }}>
                        <span style={{ flexShrink: 0 }}>{triggerLabel}</span>
                        {channel && (
                          <span style={{ fontSize: 12, opacity: 0.65, whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>
                            {channel}
                          </span>
                        )}
                      </div>
                    ),
                    title: `${triggerLabel}${channel ? `（${channel}）` : ''}`,
                  }
                })}
                optionLabelProp="triggerLabel"
                optionFilterProp="title"
                disabled={sending}
              />
            </Tooltip>
          )}
          {teamAgentOptions && teamAgentOptions.length > 1 && (
            <Tooltip title={t('appChat.chatTeamAgent')}>
              <Select
                size="small"
                variant="borderless"
                showSearch
                style={{ minWidth: 130, maxWidth: 220 }}
                value={selectedTeamAgentId ?? undefined}
                onChange={(v) => onTeamAgentChange?.(v)}
                options={teamAgentOptions.map((a) => {
                  // 下拉行带智能体头像（无头像回退首字）与描述次要文本，关闭态显示纯名称
                  const name = a.name || a.key || '-'
                  const desc = (a.description || '').trim()
                  return {
                    value: a.id,
                    agentName: name,
                    label: (
                      <div style={{ display: 'flex', alignItems: 'center', gap: 8, minWidth: 0 }}>
                        <Avatar shape="square" size={18} src={resolveStorageUrl(a.avatarObjectKey || '') || undefined}>
                          {name.slice(0, 1)}
                        </Avatar>
                        <span style={{ flexShrink: 0 }}>{name}</span>
                        {desc && (
                          <span style={{ fontSize: 12, opacity: 0.65, whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>
                            {desc}
                          </span>
                        )}
                      </div>
                    ),
                    title: name,
                  }
                })}
                optionLabelProp="agentName"
                optionFilterProp="title"
                disabled={sending}
              />
            </Tooltip>
          )}
        </div>
        <div className="moai-chat__composer-actions">
          {contextUsageNode}
          {sending ? (
            <button type="button" className="moai-chat__send" onClick={onStop} aria-label={t('appChat.stop')}>
              <StopOutlined />
            </button>
          ) : (
            <Tooltip title={attachmentsBlocking ? t('appChat.sendPendingAttachment') : ''}>
              <button
                type="button"
                className="moai-chat__send"
                onClick={onSubmit}
                disabled={sendDisabled}
                aria-label={t('appChat.send')}
              >
                <ArrowUpOutlined />
              </button>
            </Tooltip>
          )}
        </div>
      </div>
    </div>
  )
}
