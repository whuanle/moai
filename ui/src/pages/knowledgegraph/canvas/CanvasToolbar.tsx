import { useTranslation } from 'react-i18next'
import { Button, Input, Popover, Segmented, Select, Space, Tooltip } from 'antd'
import {
  CompressOutlined,
  ExpandOutlined,
  FullscreenExitOutlined,
  FullscreenOutlined,
  LinkOutlined,
  MergeOutlined,
  ReloadOutlined,
  SearchOutlined,
  SettingOutlined,
  ZoomInOutlined,
  ZoomOutOutlined,
} from '@ant-design/icons'
import { Switch } from 'antd'
import type { LayoutKind } from './types'

const LIMIT_OPTIONS = [100, 200, 500]

interface CanvasToolbarProps {
  /** 搜索模式：entity=画布内实体定位；nl=自然语言问图（Text2Cypher） */
  searchMode: 'entity' | 'nl'
  onSearchModeChange: (mode: 'entity' | 'nl') => void
  /** 客户端定位（画布内高亮居中）；nl 模式下回车触发问图 */
  onLocate: (term: string) => void
  /** 服务端关键字重载（画布内未命中时兜底，重新拉取子图） */
  onServerSearch: (term: string) => void
  /** 问图进行中（nl 模式回车后置位，完成后由父层复位） */
  asking?: boolean
  layout: LayoutKind
  onLayoutChange: (kind: LayoutKind) => void
  depth: 1 | 2
  onDepthChange: (depth: 1 | 2) => void
  depthDisabled?: boolean
  limit: number
  onLimitChange: (limit: number) => void
  onReload: () => void
  onZoomIn: () => void
  onZoomOut: () => void
  onFit: () => void
  isFullscreen: boolean
  onToggleFullscreen: () => void
  showEdgeLabels: boolean
  onToggleEdgeLabels: (value: boolean) => void
  canEdit: boolean
  selectionCount: number
  onConnect: () => void
  onMerge: () => void
  onClearSelection: () => void
}

/** 画布工具栏：搜索定位/布局切换/深度与上限/缩放适配全屏/显示设置；编辑态多选时出现连关系与合并入口 */
export function CanvasToolbar(props: CanvasToolbarProps) {
  const { t } = useTranslation()

  return (
    <Space wrap style={{ marginBottom: 12 }}>
      <Segmented
        value={props.searchMode}
        onChange={(value) => props.onSearchModeChange(value as 'entity' | 'nl')}
        options={[
          { value: 'entity', label: t('knowledgegraph.canvas.modeEntity') },
          { value: 'nl', label: t('knowledgegraph.canvas.modeAsk') },
        ]}
      />
      <Input.Search
        allowClear
        enterButton={props.searchMode === 'nl' ? t('knowledgegraph.canvas.askSubmit') : undefined}
        loading={props.searchMode === 'nl' && props.asking}
        placeholder={props.searchMode === 'nl' ? t('knowledgegraph.canvas.askPlaceholder') : t('knowledgegraph.canvas.searchLocatePlaceholder')}
        prefix={props.searchMode === 'entity' ? <SearchOutlined style={{ color: 'inherit' }} /> : undefined}
        onSearch={(value) => {
          const term = value.trim()
          if (!term) return
          props.onLocate(term)
        }}
        style={{ width: props.searchMode === 'nl' ? 320 : 230 }}
      />
      <Segmented
        value={props.layout}
        onChange={(value) => props.onLayoutChange(value as LayoutKind)}
        options={[
          { value: 'force', label: t('knowledgegraph.canvas.layoutForce') },
          { value: 'circular', label: t('knowledgegraph.canvas.layoutCircular') },
          { value: 'grid', label: t('knowledgegraph.canvas.layoutGrid') },
        ]}
      />
      <Tooltip title={props.depthDisabled ? t('knowledgegraph.canvas.depthManagedOnly') : t('knowledgegraph.canvas.depthTip')}>
        <Segmented
          value={props.depth}
          onChange={(value) => props.onDepthChange(Number(value) as 1 | 2)}
          disabled={props.depthDisabled}
          options={[
            { value: 1, label: t('knowledgegraph.canvas.depthOne') },
            { value: 2, label: t('knowledgegraph.canvas.depthTwo') },
          ]}
        />
      </Tooltip>
      <Tooltip title={t('knowledgegraph.canvasLimitTip')}>
        <Select
          value={props.limit}
          onChange={props.onLimitChange}
          options={LIMIT_OPTIONS.map((x) => ({ value: x, label: `${x}` }))}
          style={{ width: 96 }}
          popupMatchSelectWidth={false}
        />
      </Tooltip>
      <Button icon={<ReloadOutlined />} onClick={props.onReload}>
        {t('knowledgegraph.canvasReload')}
      </Button>
      <Tooltip title={t('knowledgegraph.canvas.zoomIn')}>
        <Button icon={<ZoomInOutlined />} onClick={props.onZoomIn} aria-label={t('knowledgegraph.canvas.zoomIn')} />
      </Tooltip>
      <Tooltip title={t('knowledgegraph.canvas.zoomOut')}>
        <Button icon={<ZoomOutOutlined />} onClick={props.onZoomOut} aria-label={t('knowledgegraph.canvas.zoomOut')} />
      </Tooltip>
      <Tooltip title={t('knowledgegraph.canvas.fitView')}>
        <Button icon={<CompressOutlined />} onClick={props.onFit} aria-label={t('knowledgegraph.canvas.fitView')} />
      </Tooltip>
      <Tooltip title={t(props.isFullscreen ? 'knowledgegraph.canvasFullscreenExit' : 'knowledgegraph.canvasFullscreen')}>
        <Button
          icon={props.isFullscreen ? <FullscreenExitOutlined /> : <FullscreenOutlined />}
          onClick={props.onToggleFullscreen}
          aria-label={t(props.isFullscreen ? 'knowledgegraph.canvasFullscreenExit' : 'knowledgegraph.canvasFullscreen')}
        />
      </Tooltip>
      <Popover
        trigger="click"
        content={(
          <Space direction="vertical" size={4}>
            <span style={{ fontSize: 12, opacity: 0.75 }}>{t('knowledgegraph.canvas.displaySettings')}</span>
            <Space size={8}>
              <Switch size="small" checked={props.showEdgeLabels} onChange={props.onToggleEdgeLabels} />
              <span style={{ fontSize: 13 }}>{t('knowledgegraph.canvas.showEdgeLabels')}</span>
            </Space>
          </Space>
        )}
      >
        <Tooltip title={t('knowledgegraph.canvas.displaySettings')}>
          <Button icon={<SettingOutlined />} aria-label={t('knowledgegraph.canvas.displaySettings')} />
        </Tooltip>
      </Popover>
      {props.canEdit && props.selectionCount >= 2 && (
        <>
          <Tooltip title={t('knowledgegraph.canvas.connectHint')}>
            <Button
              icon={<LinkOutlined />}
              disabled={props.selectionCount !== 2}
              onClick={props.onConnect}
            >
              {t('knowledgegraph.canvas.connect')}
            </Button>
          </Tooltip>
          <Button type="primary" danger ghost icon={<MergeOutlined />} onClick={props.onMerge}>
            {t('knowledgegraph.canvas.mergeAction', { count: props.selectionCount })}
          </Button>
          <Button onClick={props.onClearSelection}>{t('knowledgegraph.canvas.clearSelection')}</Button>
        </>
      )}
    </Space>
  )
}
