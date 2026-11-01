import type { ReactNode } from 'react'
import { Empty, Segmented, Tag, Typography, theme } from 'antd'
import { AppstoreOutlined, BarsOutlined, ThunderboltFilled } from '@ant-design/icons'
import { useTranslation } from 'react-i18next'
import { Card } from '@/design-system'
import { spacing } from '@/design-system/theme'
import { classifyLabel, type PluginClassify } from '@/api/classify'
import { formatDateTime } from '@/utils/datetime'
import { PluginAvatar } from './PluginAvatarUpload'

const { Text } = Typography

/** 插件视图类型：卡片 / 列表. */
export type PluginView = 'card' | 'list'

/** 插件列表类型标签文案（管理列表的数字类型枚举）. */
const MANAGE_TYPE_LABEL_KEY: Record<number, string> = {
  0: 'plugins.typeMcp',
  1: 'plugins.typeOpenapi',
  2: 'plugins.typeNative',
  3: 'plugins.typeTool',
}

/** 管理列表数字类型的展示文案. */
export function manageTypeLabel(t: (k: string) => string, type: number | null | undefined): string {
  if (type == null) return '-'
  return MANAGE_TYPE_LABEL_KEY[type] ? t(MANAGE_TYPE_LABEL_KEY[type]) : String(type)
}

/** 插件类型对应的 Tag 颜色（mcp/openApi 字符串或管理列表数字）. */
export function pluginTypeColor(type: string | number | null | undefined): string | undefined {
  if (type === 'mcp' || type === 0) return 'green'
  if (type === 'openApi' || type === 1) return 'orange'
  return undefined
}

/** 卡片/列表视图切换（Segmented，图标 + 文案）. */
export function PluginViewToggle({ value, onChange }: { value: PluginView; onChange: (view: PluginView) => void }) {
  const { t } = useTranslation()
  return (
    <Segmented
      value={value}
      onChange={(v) => onChange(v as PluginView)}
      aria-label={t('plugins.viewToggle')}
      options={[
        { value: 'card', label: <><AppstoreOutlined /> {t('plugins.viewCard')}</> },
        { value: 'list', label: <><BarsOutlined /> {t('plugins.viewList')}</> },
      ]}
    />
  )
}

/** 卡片网格：自适应列宽，窄屏自动降为单列. */
export function PluginCardGrid({ children }: { children: ReactNode }) {
  return (
    <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(300px, 1fr))', gap: spacing.md }}>
      {children}
    </div>
  )
}

/** 空列表占位（卡片视图用；列表视图由 DataTable 自带空态）. */
export function PluginCardEmpty() {
  const { t } = useTranslation()
  return (
    <Card>
      <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={t('plugins.empty')} style={{ padding: spacing.md }} />
    </Card>
  )
}

interface PluginCardProps {
  /** 头像 ObjectKey. */
  avatarPath?: string | null
  /** 插件标题（主标题，空时回退名称）. */
  title?: string | null
  /** 插件名称（副行独立文本节点）. */
  name: string
  /** 服务器地址（仅自定义插件，副行追加展示）. */
  server?: string | null
  /** 类型标签文案. */
  typeLabel: string
  /** 类型标签颜色. */
  typeColor?: string | null
  /** 描述（两行截断）. */
  description?: string | null
  /** 标签行内容（分类/可见性/模板等）. */
  tags?: ReactNode
  /** 使用量计数. */
  counter?: string | number | null
  /** 更新时间（审计字段，内部走 formatDateTime）. */
  updatedTime?: string | null
  /** 操作区（查看/编辑等 link 按钮 + 更多下拉）. */
  actions?: ReactNode
}

/** 插件卡片：头像 + 标题/名称、类型徽标、两行描述、标签行、使用量与操作区. */
export function PluginCard({
  avatarPath,
  title,
  name,
  server,
  typeLabel,
  typeColor,
  description,
  tags,
  counter,
  updatedTime,
  actions,
}: PluginCardProps) {
  const { token } = theme.useToken()
  return (
    <Card
      hoverable
      style={{ height: '100%' }}
      styles={{
        body: {
          display: 'flex',
          flexDirection: 'column',
          gap: spacing.xs,
          height: '100%',
          padding: spacing.md,
        },
      }}
    >
      <div style={{ display: 'flex', alignItems: 'flex-start', gap: spacing.sm }}>
        <PluginAvatar objectKey={avatarPath} title={title ?? name} size={40} />
        <div style={{ minWidth: 0, flex: 1 }}>
          <Text strong ellipsis={{ tooltip: title || name }} style={{ display: 'block' }}>
            {title || name}
          </Text>
          <Text
            type="secondary"
            ellipsis={{ tooltip: name }}
            style={{ display: 'block', fontSize: 12, fontFamily: 'monospace' }}
          >
            {name}
          </Text>
          {server && (
            <Text
              type="secondary"
              ellipsis={{ tooltip: server }}
              style={{ display: 'block', fontSize: 12, fontFamily: 'monospace' }}
            >
              {server}
            </Text>
          )}
        </div>
        <Tag color={typeColor ?? undefined} style={{ flexShrink: 0 }}>
          {typeLabel}
        </Tag>
      </div>
      <Text
        type="secondary"
        style={{
          fontSize: 13,
          minHeight: 40,
          display: '-webkit-box',
          WebkitLineClamp: 2,
          WebkitBoxOrient: 'vertical',
          overflow: 'hidden',
        }}
      >
        {description || '-'}
      </Text>
      <div style={{ display: 'flex', flexWrap: 'wrap', gap: 4, minHeight: 22, alignItems: 'center' }}>{tags}</div>
      <div
        style={{
          marginTop: 'auto',
          display: 'flex',
          alignItems: 'center',
          gap: spacing.xs,
          borderTop: `1px solid ${token.colorSplit}`,
          paddingTop: spacing.xs,
        }}
      >
        <Text type="secondary" style={{ fontSize: 12, display: 'inline-flex', alignItems: 'center', gap: 4, minWidth: 0 }}>
          <ThunderboltFilled style={{ color: token.colorWarning, flexShrink: 0 }} />
          <span style={{ fontVariantNumeric: 'tabular-nums' }}>{Number(counter ?? 0)}</span>
          <span>·</span>
          <span style={{ overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
            {formatDateTime(updatedTime)}
          </span>
        </Text>
        <div style={{ marginLeft: 'auto', display: 'flex', alignItems: 'center', flexShrink: 0 }}>{actions}</div>
      </div>
    </Card>
  )
}

interface PluginClassifyTagProps {
  classifies: PluginClassify[]
  classifyId?: number | null
  classifyName?: string | null
}

/** 分类标签：优先按 id 匹配分类（含 emoji），其次展示后端回填名称，均无时为未分类. */
export function PluginClassifyTag({ classifies, classifyId, classifyName }: PluginClassifyTagProps) {
  const { t } = useTranslation()
  const classify = classifies.find((c) => c.classifyId === classifyId)
  if (classify) return <Tag color="blue">{classifyLabel(classify)}</Tag>
  if (classifyName) return <Tag>{classifyName}</Tag>
  return <Tag color="orange">{t('plugins.classifyUncategorized')}</Tag>
}

/** 可见性标签：公开 / 私有. */
export function PluginVisibilityTag({ isPublic }: { isPublic?: boolean | null }) {
  const { t } = useTranslation()
  return <Tag color={isPublic ? 'success' : 'default'}>{isPublic ? t('plugins.isPublic') : t('plugins.notPublic')}</Tag>
}
