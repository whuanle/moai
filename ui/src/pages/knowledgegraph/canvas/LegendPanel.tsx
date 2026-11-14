import { useTranslation } from 'react-i18next'
import { Button, Space, theme } from 'antd'

export interface LegendTypeItem {
  key: string
  name: string
  color: string
  count: number
}

interface LegendPanelProps {
  types: LegendTypeItem[]
  /** 被隐藏的类型 key 集合（空集=全部显示） */
  hidden: Set<string>
  onToggle: (key: string) => void
  onShowAll: () => void
}

/** 图例面板（画布左下角浮动）：类型 + 数量 + 多选显隐，内存过滤不重载 */
export function LegendPanel({ types, hidden, onToggle, onShowAll }: LegendPanelProps) {
  const { t } = useTranslation()
  const { token } = theme.useToken()
  if (types.length === 0) return null

  return (
    <div
      style={{
        position: 'absolute',
        left: 12,
        bottom: 12,
        zIndex: 3,
        maxWidth: 260,
        padding: '8px 10px',
        borderRadius: 8,
        border: `1px solid ${token.colorBorderSecondary}`,
        background: token.colorBgContainer,
        boxShadow: token.boxShadowTertiary,
        fontSize: 12,
      }}
    >
      <Space direction="vertical" size={4}>
        {types.map((item) => {
          const isHidden = hidden.has(item.key)
          return (
            <Button
              key={item.key}
              type="text"
              size="small"
              onClick={() => onToggle(item.key)}
              style={{ opacity: isHidden ? 0.45 : 1, padding: '0 4px', height: 22, fontSize: 12 }}
            >
              <span
                aria-hidden
                style={{
                  display: 'inline-block',
                  width: 8,
                  height: 8,
                  borderRadius: 4,
                  marginRight: 6,
                  background: item.color,
                  textDecoration: isHidden ? 'line-through' : 'none',
                }}
              />
              {item.name}
              <span style={{ opacity: 0.55, marginLeft: 6 }}>{item.count}</span>
            </Button>
          )
        })}
        {hidden.size > 0 && (
          <Button type="link" size="small" style={{ height: 22, padding: 0, fontSize: 12 }} onClick={onShowAll}>
            {t('knowledgegraph.canvas.showAllTypes')}
          </Button>
        )}
      </Space>
    </div>
  )
}
