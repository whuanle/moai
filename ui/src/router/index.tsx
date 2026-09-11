import { createBrowserRouter, Navigate } from 'react-router'
import { AppLayout } from '@/layouts/AppLayout'
import { RequireAuth } from '@/auth/RequireAuth'
import { Login } from '@/pages/auth/Login'
import { OAuthLogin } from '@/pages/auth/OAuthLogin'
import { Register } from '@/pages/auth/Register'
import { Dashboard } from '@/pages/Dashboard'
import { DesignSystemPreview } from '@/pages/DesignSystemPreview'
import { Settings } from '@/pages/settings/Settings'
import { AccountSettings } from '@/pages/account/AccountSettings'
import { OauthConnect } from '@/pages/oauthconnect/OauthConnect'
import { Users } from '@/pages/users/Users'
import { AdminTeams } from '@/pages/admin/AdminTeams'
import { Models } from '@/pages/ai/Models'
import { Teams } from '@/pages/teams/Teams'
import { TeamManage } from '@/pages/teams/TeamManage'
import { AppManage } from '@/pages/teams/apps/AppManage'
import { Wiki } from '@/pages/wiki/Wiki'
import { WikiDetail } from '@/pages/wiki/WikiDetail'
import { WikiDocumentDetail } from '@/pages/wiki/WikiDocumentDetail'
import { Plugins } from '@/pages/plugins/Plugins'
import { PluginTemplates } from '@/pages/plugins/PluginTemplates'
import { ClassifyPage } from '@/pages/classify/Classify'

export const router = createBrowserRouter([
  { path: '/login', element: <Login /> },
  { path: '/oauth_login', element: <OAuthLogin /> },
  { path: '/register', element: <Register /> },
  { path: '/design-system', element: <DesignSystemPreview /> },
  {
    path: '/',
    element: (
      <RequireAuth>
        <AppLayout />
      </RequireAuth>
    ),
    children: [
      { index: true, element: <Navigate to="/dashboard" replace /> },
      { path: 'dashboard', element: <Dashboard /> },
      { path: 'account', element: <AccountSettings /> },
      { path: 'users', element: <Users /> },
      { path: 'admin/teams', element: <AdminTeams /> },
      { path: 'team', element: <Teams /> },
      { path: 'team/:id/:section?', element: <TeamManage /> },
      { path: 'team/:teamId/app/:appId', element: <AppManage /> },
      { path: 'team/:teamId/wiki/:wikiId/:section?', element: <WikiDetail /> },
      { path: 'team/:teamId/wiki/:wikiId/document/:documentId/:section?', element: <WikiDocumentDetail /> },
      { path: 'wiki', element: <Wiki /> },
      { path: 'settings', element: <Settings /> },
      { path: 'oauthconnect', element: <OauthConnect /> },
      { path: 'models', element: <Models /> },
      { path: 'plugin', element: <Plugins /> },
      { path: 'plugin/templates', element: <PluginTemplates /> },
      { path: 'classify', element: <ClassifyPage /> },
      // 其它专用页面（/xxx）在此追加
      { path: '*', element: <Navigate to="/dashboard" replace /> },
    ],
  },
])
