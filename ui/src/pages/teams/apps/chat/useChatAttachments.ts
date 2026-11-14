import { useCallback, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { feedback } from '@/design-system'
import { extractChatAttachment } from '@/api/app'
import { uploadChatFile } from '@/utils/storage'
import {
  isImageAttachment,
  isSupportedAttachment,
  MAX_ATTACHMENTS,
  MAX_ATTACHMENT_SIZE,
  type ChatAttachmentDraft,
} from './attachment'

/**
 * 对话输入卡附件状态机：选择 → 直传存储 →（文档）文本提取 → 就绪；失败标记在 chip 上由用户移除。
 * 供正式对话页（AppChat）与配置页调试面板（AppDebugChat）复用；展示与发送拼装见 ChatComposer/buildOutgoingText。
 */
export function useChatAttachments() {
  const { t } = useTranslation()
  const [attachments, setAttachments] = useState<ChatAttachmentDraft[]>([])

  // 选择附件：直传存储 →（文档）文本提取 → 就绪；失败标记在 chip 上由用户移除
  const handleFiles = useCallback(
    async (files: FileList | null) => {
      if (!files || files.length === 0) return
      const list = Array.from(files)
      const accepted = list.filter((f) => {
        if (!isSupportedAttachment(f.name)) {
          feedback.error(t('appChat.attachmentUnsupportedType', { name: f.name }))
          return false
        }
        if (f.size > MAX_ATTACHMENT_SIZE) {
          feedback.error(t('appChat.attachmentTooLarge', { name: f.name }))
          return false
        }
        return true
      })
      if (accepted.length === 0) return

      const slots = MAX_ATTACHMENTS - attachments.length
      if (slots <= 0) {
        feedback.warning(t('appChat.attachmentTooMany', { count: MAX_ATTACHMENTS }))
        return
      }
      const drafts: ChatAttachmentDraft[] = accepted.slice(0, slots).map((f) => ({
        id: crypto.randomUUID(),
        name: f.name,
        size: f.size,
        objectKey: '',
        url: '',
        isImage: isImageAttachment(f.name),
        status: 'uploading',
        markdown: '',
        truncated: false,
      }))
      const overflow = accepted.length - drafts.length
      if (overflow > 0) feedback.warning(t('appChat.attachmentTooMany', { count: MAX_ATTACHMENTS }))
      setAttachments((prev) => [...prev, ...drafts])

      await Promise.all(
        drafts.map(async (draft, index) => {
          const patch = (updater: (a: ChatAttachmentDraft) => ChatAttachmentDraft) => {
            setAttachments((prev) => prev.map((a) => (a.id === draft.id ? updater(a) : a)))
          }
          const file = accepted[index]
          try {
            const uploaded = await uploadChatFile(file)
            if (draft.isImage) {
              patch((a) => ({ ...a, ...uploaded, status: 'ready' }))
              return
            }
            patch((a) => ({ ...a, ...uploaded, status: 'extracting' }))
            const extracted = await extractChatAttachment(uploaded.objectKey, file.name)
            patch((a) => ({ ...a, status: 'ready', markdown: extracted.markdown, truncated: extracted.truncated }))
          } catch {
            patch((a) => ({ ...a, status: 'failed' }))
          }
        }),
      )
    },
    [attachments.length, t],
  )

  const removeAttachment = useCallback((id: string) => {
    setAttachments((prev) => prev.filter((a) => a.id !== id))
  }, [])

  const reset = useCallback(() => {
    setAttachments([])
  }, [])

  return { attachments, handleFiles, removeAttachment, reset }
}
