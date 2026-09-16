import { Fragment, type ReactNode } from 'react'
import { Button, Divider, Tooltip } from 'antd'
import {
  AlignLeftOutlined,
  BoldOutlined,
  CheckSquareOutlined,
  CodeOutlined,
  FileMarkdownOutlined,
  ItalicOutlined,
  LinkOutlined,
  MinusOutlined,
  OrderedListOutlined,
  StrikethroughOutlined,
  TableOutlined,
  UnorderedListOutlined,
} from '@ant-design/icons'
import { useTranslation } from 'react-i18next'
import type { MarkdownAction } from './markdown'

const GROUPS: MarkdownAction[][] = [
  ['bold', 'italic', 'strike'],
  ['h1', 'h2', 'h3'],
  ['ul', 'ol', 'task'],
  ['quote', 'inlineCode', 'codeBlock'],
  ['link', 'table', 'hr'],
]

const TEXT_ONLY: Partial<Record<MarkdownAction, string>> = { h1: 'H1', h2: 'H2', h3: 'H3' }

const ICONS: Partial<Record<MarkdownAction, ReactNode>> = {
  bold: <BoldOutlined />,
  italic: <ItalicOutlined />,
  strike: <StrikethroughOutlined />,
  ul: <UnorderedListOutlined />,
  ol: <OrderedListOutlined />,
  task: <CheckSquareOutlined />,
  quote: <AlignLeftOutlined />,
  inlineCode: <CodeOutlined />,
  codeBlock: <FileMarkdownOutlined />,
  link: <LinkOutlined />,
  table: <TableOutlined />,
  hr: <MinusOutlined />,
}

const LABEL_KEYS: Record<MarkdownAction, string> = {
  bold: 'prompt.toolBold',
  italic: 'prompt.toolItalic',
  strike: 'prompt.toolStrike',
  h1: 'prompt.toolH1',
  h2: 'prompt.toolH2',
  h3: 'prompt.toolH3',
  ul: 'prompt.toolUl',
  ol: 'prompt.toolOl',
  task: 'prompt.toolTask',
  quote: 'prompt.toolQuote',
  inlineCode: 'prompt.toolInlineCode',
  codeBlock: 'prompt.toolCodeBlock',
  link: 'prompt.toolLink',
  table: 'prompt.toolTable',
  hr: 'prompt.toolHr',
}

/** Markdown 编辑工具栏：分组图标按钮，点击把动作交给编辑器内核处理选区 */
export function MarkdownToolbar({ onAction }: { onAction: (action: MarkdownAction) => void }) {
  const { t } = useTranslation()

  return (
    <div style={{ display: 'flex', alignItems: 'center', flexWrap: 'wrap', rowGap: 4 }}>
      {GROUPS.map((group, groupIndex) => (
        <Fragment key={groupIndex}>
          {groupIndex > 0 && <Divider type="vertical" style={{ marginInline: 4 }} />}
          {group.map((action) => {
            const label = t(LABEL_KEYS[action])
            const text = TEXT_ONLY[action]
            return (
              <Tooltip key={action} title={label}>
                <Button
                  type="text"
                  size="small"
                  aria-label={label}
                  onClick={() => onAction(action)}
                  icon={text ? undefined : ICONS[action]}
                  style={text ? { minWidth: 30, fontWeight: 600, fontSize: 12 } : undefined}
                >
                  {text}
                </Button>
              </Tooltip>
            )
          })}
        </Fragment>
      ))}
    </div>
  )
}
