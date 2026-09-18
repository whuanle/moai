import ReactMarkdown from 'react-markdown'
import remarkGfm from 'remark-gfm'
import { CopyOutlined } from '@ant-design/icons'
import { Avatar, Button, Divider, Modal, Typography } from 'antd'
import { useTranslation } from 'react-i18next'
import { feedback } from '@/design-system'
import { resolveStorageUrl } from '@/utils/storage'
import type { PromptDetail } from '@/api/prompt'

const { Paragraph, Text } = Typography

/** 提示词详情弹窗：头像 + 描述 + 横线分隔的 Markdown 内容（我的提示词/团队分区/市场共用） */
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

  const handleCopy = async () => {
    try {
      await navigator.clipboard.writeText(detail?.content ?? '')
      feedback.success(t('common.copySuccess'))
    } catch {
      // 剪贴板不可用时忽略
    }
  }

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
      width={860}
      maskClosable={false}
    >
      {detail && (
        <>
          {detail.description && <Paragraph type="secondary">{detail.description}</Paragraph>}
          <Divider style={{ margin: '12px 0 16px' }} />
          <div
            style={{
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'space-between',
              gap: 12,
              marginBottom: 8,
            }}
          >
            <Text strong>{t('prompt.content')}</Text>
            <Button type="text" size="small" icon={<CopyOutlined />} onClick={() => void handleCopy()}>
              {t('common.copy')}
            </Button>
          </div>
          <div style={{ maxHeight: '60vh', overflowY: 'auto' }}>
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
