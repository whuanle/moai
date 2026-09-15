import ReactMarkdown from 'react-markdown'
import remarkGfm from 'remark-gfm'
import { Avatar, Modal, Typography } from 'antd'
import { useTranslation } from 'react-i18next'
import { resolveStorageUrl } from '@/utils/storage'
import type { PromptDetail } from '@/api/prompt'

const { Paragraph, Text } = Typography

/** 提示词详情弹窗：头像 + 描述 + Markdown 渲染内容（我的提示词/团队分区/市场共用） */
export function PromptDetailModal({
  open,
  detail,
  onClose,
}: {
  open: boolean
  detail: PromptDetail | null
  onClose: () => void
}) {
  const { t } = useTranslation()

  return (
    <Modal
      open={open}
      title={
        detail ? (
          <span style={{ display: 'inline-flex', alignItems: 'center', gap: 10 }}>
            <Avatar size={28} src={detail.avatarPath ? resolveStorageUrl(detail.avatarPath) : undefined}>
              {(detail.name ?? '?').slice(0, 1).toUpperCase()}
            </Avatar>
            {detail.name}
          </span>
        ) : (
          t('prompt.detailTitle')
        )
      }
      onCancel={onClose}
      footer={null}
      width={720}
      maskClosable={false}
    >
      {detail && (
        <>
          {detail.description && (
            <Paragraph type="secondary">{detail.description}</Paragraph>
          )}
          <Paragraph>
            <Text copyable={{ text: detail.content ?? '', tooltips: [t('common.copy'), t('common.copySuccess')] }} strong>
              {t('prompt.content')}
            </Text>
          </Paragraph>
          <div style={{ border: '1px solid rgba(128,128,128,0.25)', borderRadius: 8, padding: '8px 16px', maxHeight: 420, overflow: 'auto' }}>
            <ReactMarkdownPreview content={detail.content ?? ''} />
          </div>
        </>
      )}
    </Modal>
  )
}

/** Markdown 渲染（GFM），供详情弹窗与编辑器预览复用 */
export function ReactMarkdownPreview({ content }: { content: string }) {
  return (
    <div className="markdown-body" style={{ wordBreak: 'break-word' }}>
      <ReactMarkdown remarkPlugins={[remarkGfm]}>{content}</ReactMarkdown>
    </div>
  )
}
