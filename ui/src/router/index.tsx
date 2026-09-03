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
import { Models } from '@/pages/ai/Models'
import { Teams } from '@/pages/teams/Teams'
import { Wiki } from '@/pages/wiki/Wiki'
import { WikiDocuments } from '@/pages/wiki/WikiDocuments'
import { Variables } from '@/pages/variables/Variables'

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
      { path: 'team', element: <Teams /> },
      { path: 'wiki', element: <Wiki /> },
      { path: 'wiki/:id', element: <WikiDocuments /> },
      { path: 'variable', element: <Variables /> },
      { path: 'settings', element: <Settings /> },
      { path: 'oauthconnect', element: <OauthConnect /> },
      { path: 'models', element: <Models /> },
      // 其它专用页面（/xxx）在此追加
      { path: '*', element: <Navigate to="/dashboard" replace /> },
    ],
  },
])
