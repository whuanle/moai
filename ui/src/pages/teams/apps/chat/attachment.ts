/**
 * 对话附件：上传草稿状态与「用户消息 + 附件内容」的拼接/解析。
 * <para>
 * 发送时附件提取文本以 <c>moai-attachment</c> 标记块拼进用户消息（AG-UI 链路纯文本），
 * 渲染时再解析出附件 chip，保证刷新加载的历史消息与实时消息展示一致。
 * </para>
 */

import type { ComponentType } from 'react'
import {
  CodeOutlined,
  FileExcelOutlined,
  FileImageOutlined,
  FileMarkdownOutlined,
  FilePdfOutlined,
  FilePptOutlined,
  FileTextOutlined,
  FileWordOutlined,
} from '@ant-design/icons'

/** 输入框中的附件草稿 */
export interface ChatAttachmentDraft {
  id: string
  name: string
  size: number
  /** 存储对象 key（public/chat 前缀） */
  objectKey: string
  /** 免登录下载地址（/static 中转） */
  url: string
  isImage: boolean
  /** uploading=直传中；extracting=文本提取中；ready=可发送；failed=上传或提取失败 */
  status: 'uploading' | 'extracting' | 'ready' | 'failed'
  /** 提取出的 markdown（图片附件为空） */
  markdown: string
  truncated: boolean
}

/** 历史消息解析出的附件 */
export interface ParsedAttachment {
  name: string
  /** 文档附件为提取的 markdown；图片附件为下载地址 */
  content: string
}

const ATTACHMENT_TAG = 'moai-attachment'
// name 必填；objectKey 仅图片附件携带（后端据此从存储读取字节做多模态注入），解析时兼容无该属性的历史块
const ATTACHMENT_RE = /<moai-attachment name="([^"]*)"(?: objectKey="([^"]*)")?>\n?([\s\S]*?)\n?<\/moai-attachment>/g

const IMAGE_EXTENSIONS = ['.jpg', '.jpeg', '.png', '.gif', '.bmp', '.webp', '.svg']

/** 单条消息最多携带的附件数 */
export const MAX_ATTACHMENTS = 5

/** 附件大小上限（与后端 PreUploadChatFileCommand 一致） */
export const MAX_ATTACHMENT_SIZE = 20 * 1024 * 1024

/** 受支持的附件扩展名（文档走提取，图片经后端多模态注入） */
export function isSupportedAttachment(fileName: string): boolean {
  const lower = fileName.toLowerCase()
  const ext = lower.slice(lower.lastIndexOf('.'))
  if (!ext || ext === lower) return false
  if (IMAGE_EXTENSIONS.includes(ext)) return true
  return [
    '.md', '.pdf', '.doc', '.docx', '.xls', '.xlsx', '.ppt', '.pptx', '.txt',
    '.rtf', '.odt', '.ods', '.odp', '.csv', '.json', '.xml', '.html', '.htm',
    '.epub', '.mobi', '.eml', '.msg', '.tex',
  ].includes(ext)
}

export function isImageAttachment(fileName: string): boolean {
  const lower = fileName.toLowerCase()
  const ext = lower.slice(lower.lastIndexOf('.'))
  return IMAGE_EXTENSIONS.includes(ext)
}

/** 文档扩展名 → 类型图标（Office/PDF/Markdown/代码类各有专属图标，其余落通用文本图标） */
const FILE_ICONS: Record<string, ComponentType> = {
  '.pdf': FilePdfOutlined,
  '.doc': FileWordOutlined,
  '.docx': FileWordOutlined,
  '.rtf': FileWordOutlined,
  '.odt': FileWordOutlined,
  '.xls': FileExcelOutlined,
  '.xlsx': FileExcelOutlined,
  '.ods': FileExcelOutlined,
  '.csv': FileExcelOutlined,
  '.ppt': FilePptOutlined,
  '.pptx': FilePptOutlined,
  '.odp': FilePptOutlined,
  '.md': FileMarkdownOutlined,
  '.json': CodeOutlined,
  '.xml': CodeOutlined,
  '.html': CodeOutlined,
  '.htm': CodeOutlined,
  '.tex': CodeOutlined,
}

/** 按文件名取附件类型图标（图片名返回图片图标，作为缩略图不可用时的兜底） */
export function getAttachmentFileIcon(fileName: string): ComponentType {
  const lower = fileName.toLowerCase()
  const ext = lower.slice(lower.lastIndexOf('.'))
  if (IMAGE_EXTENSIONS.includes(ext)) return FileImageOutlined
  return FILE_ICONS[ext] ?? FileTextOutlined
}

/** 从附件展示内容解析图片地址：新格式为裸 URL，历史格式为 [图片附件](url)；非图片返回 null */
export function attachmentImageSrc(content: string): string | null {
  const trimmed = content.trim()
  if (/^https?:\/\//i.test(trimmed)) return trimmed
  const match = /^\[图片附件\]\((https?:\/\/[^\s)]+)\)$/.exec(trimmed)
  return match ? match[1] : null
}

function escapeAttr(value: string): string {
  return value.replace(/&/g, '&amp;').replace(/"/g, '&quot;').replace(/</g, '&lt;')
}

function unescapeAttr(value: string): string {
  return value.replace(/&lt;/g, '<').replace(/&quot;/g, '"').replace(/&amp;/g, '&')
}

/** 是否有附件仍在处理中（上传/提取），处理完才能发送 */
export function hasPendingAttachment(attachments: ChatAttachmentDraft[]): boolean {
  return attachments.some((a) => a.status === 'uploading' || a.status === 'extracting')
}

/** 拼接发送文本：用户输入 + 各附件标记块 */
export function buildOutgoingText(text: string, attachments: ChatAttachmentDraft[]): string {
  const ready = attachments.filter((a) => a.status === 'ready')
  if (ready.length === 0) return text
  const blocks = ready.map((a) => {
    // 图片不提取文本：块内放裸下载地址（气泡 chip 点击可打开），并携带 objectKey 供后端读取字节注入多模态
    const content = a.isImage ? a.url : (a.markdown || '(空文档)')
    const attrs = a.isImage ? ` objectKey="${escapeAttr(a.objectKey)}"` : ''
    return `<${ATTACHMENT_TAG} name="${escapeAttr(a.name)}"${attrs}>\n${content.trim()}\n</${ATTACHMENT_TAG}>`
  })
  const head = text.trim()
  return head ? `${head}\n\n${blocks.join('\n\n')}` : blocks.join('\n\n')
}

/** 解析消息文本中的附件标记块，返回展示文本与附件列表 */
export function parseAttachmentMessage(content: string): { text: string; attachments: ParsedAttachment[] } {
  const attachments: ParsedAttachment[] = []
  // 回调依次收到 name / objectKey（可缺席）/ body 三个捕获组
  const text = content.replace(ATTACHMENT_RE, (_all, name: string, _objectKey: string | undefined, body: string) => {
    attachments.push({ name: unescapeAttr(name), content: body.trim() })
    return ''
  })
  return { text: text.replace(/\n{3,}/g, '\n\n').trim(), attachments }
}

/** 附件大小展示（KB/MB） */
export function formatAttachmentSize(size: number): string {
  if (size >= 1024 * 1024) return `${(size / 1024 / 1024).toFixed(1)}MB`
  if (size >= 1024) return `${Math.max(1, Math.round(size / 1024))}KB`
  return `${size}B`
}
