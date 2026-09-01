import { createBrowserRouter, Navigate } from 'react-router'
import { AppLayout } from '@/layouts/AppLayout'
import { RequireAuth } from '@/auth/RequireAuth'
import { Login } from '@/pages/auth/Login'
import { Register } from '@/pages/auth/Register'
import { Dashboard } from '@/pages/Dashboard'
import { DesignSystemPreview } from '@/pages/DesignSystemPreview'
import { Settings } from '@/pages/settings/Settings'
import { OauthConnect } from '@/pages/oauthconnect/OauthConnect'

export const router = createBrowserRouter([
  { path: '/login', element: <Login /> },
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
      { path: 'settings', element: <Settings /> },
      { path: 'oauthconnect', element: <OauthConnect /> },
      // 其它专用页面（/xxx）在此追加
      { path: '*', element: <Navigate to="/dashboard" replace /> },
    ],
  },
])
