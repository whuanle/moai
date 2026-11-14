import { ArrowLeftOutlined } from '@ant-design/icons'
import { Avatar, Button, Typography } from 'antd'
import { useNavigate } from 'react-router'
import { spacing } from '@/design-system/theme'

const { Text } = Typography

export interface SiderHeaderProps {
  /** 资源名称（头像右侧强文本） */
  name?: string | null
  /** 资源头像地址（已 resolveStorageUrl 的完整 URL）；缺省回退名称首字符 */
  avatarSrc?: string | null
  /** 头像加载失败/缺省时的图标兜底（如 BookOutlined）；缺省用名称首字符 */
  avatarFallback?: React.ReactNode
  /** 返回目标路由（上级列表页） */
  backTo: string
  /** 返回按钮文案（如「返回知识库」） */
  backLabel: string
}

/**
 * 侧栏收敛型页面（团队工作台/应用工作台/知识库详情/知识图谱详情）左栏顶部身份区：
 * 仿首页侧边栏头部两段式——上段资源头像+名称，下段整宽「← 返回xx」文字按钮。
 * 替代「返回链接 + 资源名挤同一行」的旧形态；无分区菜单的三级编辑页仍用 BackLink。
 */
export function SiderHeader({ name, avatarSrc, avatarFallback, backTo, backLabel }: SiderHeaderProps) {
  const navigate = useNavigate()
  const displayName = name ?? ''
  return (
    <div style={{ padding: `0 ${spacing.xs}px ${spacing.sm}px` }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: spacing.sm, padding: `0 ${spacing.xs}px` }}>
        <Avatar
          shape="square"
          size={30}
          src={avatarSrc || undefined}
          alt={displayName || undefined}
        >
          {avatarFallback ?? (displayName.slice(0, 1).toUpperCase() || '?')}
        </Avatar>
        <Text strong ellipsis style={{ flex: 1, minWidth: 0, fontSize: 15 }}>
          {displayName}
        </Text>
      </div>
      <Button
        block
        type="text"
        icon={<ArrowLeftOutlined aria-hidden />}
        onClick={() => navigate(backTo)}
        style={{ justifyContent: 'flex-start', marginTop: spacing.xs }}
      >
        {backLabel}
      </Button>
    </div>
  )
}
