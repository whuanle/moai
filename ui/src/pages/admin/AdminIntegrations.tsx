import { Navigate } from 'react-router'
import './AdminIntegrations.css'
import { Page } from '@/design-system'
import { useAppStore } from '@/store/app'
import { AccessAppsPanel } from '@/pages/admin/AdminAccessApps'

/**
 * 应用接入中心（仅 root）：子系统与应用接入同一列表、同一套接口。
 * 子系统=MoAI 生态独立部署的服务（委托 SSO 回调/团队组织授权/心跳在线）；
 * 应用接入=交给外部系统（ERP/OA 等）的凭证，经 /api/subsys 系统资源协作面读写组织/用户/团队。
 */
export function AdminIntegrations() {
  const isRoot = useAppStore((state) => state.userInfo?.isRoot === true)

  if (!isRoot) {
    return <Navigate to="/apps" replace />
  }

  return (
    <Page>
      <div
        style={{
          height: 'calc(100vh - 48px)',
          display: 'flex',
          flexDirection: 'column',
        }}
      >
        <AccessAppsPanel />
      </div>
    </Page>
  )
}
