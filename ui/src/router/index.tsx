import { createBrowserRouter, Navigate } from 'react-router'
import { AppLayout } from '@/layouts/AppLayout'
import { RequireAuth } from '@/auth/RequireAuth'
import { Login } from '@/pages/auth/Login'
import { OAuthLogin } from '@/pages/auth/OAuthLogin'
import { Register } from '@/pages/auth/Register'
import { Dashboard } from '@/pages/Dashboard'
import { AppPlaza } from '@/pages/apps/AppPlaza'
import { DesignSystemPreview } from '@/pages/DesignSystemPreview'
import { Settings } from '@/pages/settings/Settings'
import { AccountSettings } from '@/pages/account/AccountSettings'
import { OauthConnect } from '@/pages/oauthconnect/OauthConnect'
import { Users } from '@/pages/users/Users'
import { AdminTeams } from '@/pages/admin/AdminTeams'
import { Publications } from '@/pages/publications/Publications'
import { Models } from '@/pages/ai/Models'
import { Teams } from '@/pages/teams/Teams'
import { TeamManage } from '@/pages/teams/TeamManage'
import { AppWorkspace } from '@/pages/teams/apps/AppWorkspace'
import { AppChat } from '@/pages/teams/apps/AppChat'
import { WikiDetail } from '@/pages/wiki/WikiDetail'
import { WikiDocumentDetail } from '@/pages/wiki/WikiDocumentDetail'
import { KnowledgeGraphDetail } from '@/pages/knowledgegraph/KnowledgeGraphDetail'
import { Plugins } from '@/pages/plugins/Plugins'
import { PluginTemplates } from '@/pages/plugins/PluginTemplates'
import { Skills } from '@/pages/skills/Skills'
import { ClassifyPage } from '@/pages/classify/Classify'
import { PromptCenter } from '@/pages/prompts/PromptCenter'
import { PromptEditor } from '@/pages/prompts/PromptEditor'

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
      { path: 'apps', element: <AppPlaza /> },
      { path: 'prompts', element: <PromptCenter /> },
      { path: 'prompts/new', element: <PromptEditor /> },
      { path: 'prompts/:promptId/edit', element: <PromptEditor /> },
      { path: 'prompt-market', element: <PromptCenter /> },
      { path: 'skill-market', element: <Skills /> },
      { path: 'account', element: <AccountSettings /> },
      { path: 'users', element: <Users /> },
      { path: 'admin/teams', element: <AdminTeams /> },
      { path: 'publications', element: <Publications /> },
      { path: 'team', element: <Teams /> },
      { path: 'team/:id/:section?', element: <TeamManage /> },
      { path: 'team/:teamId/prompt/new', element: <PromptEditor /> },
      { path: 'team/:teamId/prompt/:promptId/edit', element: <PromptEditor /> },
      { path: 'team/:teamId/app/:appId/:section?', element: <AppWorkspace /> },
      { path: 'team/:teamId/app/:appId/chat', element: <AppChat /> },
      { path: 'team/:teamId/wiki/:wikiId/:section?', element: <WikiDetail /> },
      { path: 'team/:teamId/wiki/:wikiId/document/:documentId/:section?', element: <WikiDocumentDetail /> },
      { path: 'team/:teamId/kg/:graphId/:section?', element: <KnowledgeGraphDetail /> },
      // 知识库/知识图谱入口统一收敛到团队详情分区，全局路由重定向到团队列表
      { path: 'wiki', element: <Navigate to="/team" replace /> },
      { path: 'knowledge-graph', element: <Navigate to="/team" replace /> },
      { path: 'settings', element: <Settings /> },
      { path: 'oauthconnect', element: <OauthConnect /> },
      { path: 'models', element: <Models /> },
      { path: 'plugin', element: <Plugins /> },
      { path: 'plugin/templates', element: <PluginTemplates /> },
      { path: 'skills', element: <Skills /> },
      { path: 'classify', element: <ClassifyPage /> },
      // 其它专用页面（/xxx）在此追加
      { path: '*', element: <Navigate to="/dashboard" replace /> },
    ],
  },
])
