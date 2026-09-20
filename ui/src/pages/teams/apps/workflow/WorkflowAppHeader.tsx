/**
 * 流程应用工作台共享头部：返回 + 应用名/状态行 + 居中 Tab（设计/调试/配置）+ 右侧操作区。
 * 设计器（design 分区）与全屏外壳（调试/配置/运行历史分区）共用，保证三个视图布局一致。
 */

import type { ReactNode } from 'react'
import { Button, Segmented, Tooltip } from 'antd'
import { ArrowLeftOutlined } from '@ant-design/icons'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router'

export type WorkflowAppTabKey = 'design' | 'debug' | 'info'

export function WorkflowAppHeader({
  teamId,
  appId,
  appName,
  statusLine,
  activeKey,
  right,
}: {
  teamId: number
  appId: string
  appName?: string
  /** 状态行内容（草稿/已发布圆点、版本、未保存等），由调用方按视图拼装 */
  statusLine: ReactNode
  /** 当前激活 Tab；运行历史/访问点等非 Tab 分区传空串（不高亮任何项） */
  activeKey: WorkflowAppTabKey | ''
  /** 右侧操作区（设计器为 运行历史/调试运行/保存/发布，外壳为 运行历史） */
  right?: ReactNode
}) {
  const { t } = useTranslation()
  const navigate = useNavigate()

  return (
    <div className="wf-header">
      <div className="wf-header-left">
        <Tooltip title={t('appManage.backToList')}>
          <Button type="text" icon={<ArrowLeftOutlined />} onClick={() => navigate(`/team/${teamId}/apps`)} />
        </Tooltip>
        <div className="wf-header-titles">
          <div className="wf-header-name">{appName || t('workflowDesigner.title')}</div>
          <div className="wf-header-status">{statusLine}</div>
        </div>
      </div>
      <Segmented
        className="wf-header-tabs"
        value={activeKey}
        options={[
          { value: 'design', label: t('appWorkspace.tabDesign') },
          { value: 'debug', label: t('appWorkspace.menuDebug') },
          { value: 'info', label: t('appWorkspace.menuConfig') },
        ]}
        onChange={(v) => {
          if (v === activeKey) return
          navigate(`/team/${teamId}/app/${appId}/${v}`)
        }}
      />
      {right}
    </div>
  )
}
