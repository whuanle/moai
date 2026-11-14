import { Tag, Tooltip } from 'antd'
import { useTranslation } from 'react-i18next'
import type { AppVisibleScope, VisibleOrgGrantInfo } from '@/api/publication'

interface VisibleScopeTagProps {
  /** 可视范围：all=全员可见 organization=按组织限定（空值按全员回显） */
  visibleScope?: AppVisibleScope | null
  /** 按组织限定时的组织授权清单（含组织名称） */
  orgGrants?: VisibleOrgGrantInfo[] | null
}

/**
 * 可视范围只读标签（全局复用）：全员可见灰 Tag；按组织限定紫 Tag + Tooltip 展开组织名与方向
 * （方向文案复用 grantScope.orgSelf/orgUp/orgDown）。应用上架申请记录与上架审批页共用。
 */
export function VisibleScopeTag({ visibleScope, orgGrants }: VisibleScopeTagProps) {
  const { t } = useTranslation()
  if ((visibleScope ?? 'all') === 'all') return <Tag>{t('appManage.visibleScopeAll')}</Tag>
  const names = (orgGrants ?? [])
    .map((x) => `${x.roleName || `#${x.orgRoleId}`}${x.scope ? `（${t(`grantScope.org${x.scope.charAt(0).toUpperCase()}${x.scope.slice(1)}`)}）` : ''}`)
    .join('、')
  return (
    <Tooltip title={names || t('appManage.visibleScopeOrg')}>
      <Tag color="purple">{t('appManage.visibleScopeOrg')}</Tag>
    </Tooltip>
  )
}
