import { createBrowserRouter, Navigate } from 'react-router'
import { AppLayout } from '@/layouts/AppLayout'
import { RequireAuth } from '@/auth/RequireAuth'
import { Login } from '@/pages/auth/Login'
import { OAuthLogin } from '@/pages/auth/OAuthLogin'
import { Register } from '@/pages/auth/Register'
import { ForgotPassword } from '@/pages/auth/ForgotPassword'
import { AppPlaza } from '@/pages/apps/AppPlaza'
import { DesignSystemPreview } from '@/pages/DesignSystemPreview'
import { Settings } from '@/pages/settings/Settings'
import { AccountSettings } from '@/pages/account/AccountSettings'
import { OauthConnect } from '@/pages/oauthconnect/OauthConnect'
import { Users } from '@/pages/users/Users'
import { AdminTeams } from '@/pages/admin/AdminTeams'
import { AdminIntegrations } from '@/pages/admin/AdminIntegrations'
import { Publications } from '@/pages/publications/Publications'
import { Models } from '@/pages/ai/Models'
import { Teams } from '@/pages/teams/Teams'
import { TeamManage } from '@/pages/teams/TeamManage'
import { AppWorkspace } from '@/pages/teams/apps/AppWorkspace'
import { AppChat } from '@/pages/teams/apps/AppChat'
import { MyMemoryPage } from '@/pages/teams/apps/MyMemoryPage'
import { MemoryManagement } from '@/pages/memory/MemoryManagement'
import { WikiDetail } from '@/pages/wiki/WikiDetail'
import { WikiDocumentDetail } from '@/pages/wiki/WikiDocumentDetail'
import { KnowledgeGraphDetail } from '@/pages/knowledgegraph/KnowledgeGraphDetail'
import { Plugins } from '@/pages/plugins/Plugins'
import { PluginTemplates } from '@/pages/plugins/PluginTemplates'
import { Skills } from '@/pages/skills/Skills'
import { ClassifyPage } from '@/pages/classify/Classify'
import { PromptCenter } from '@/pages/prompts/PromptCenter'
import { PromptEditor } from '@/pages/prompts/PromptEditor'
import { SsoAuthorize } from '@/pages/sso/SsoAuthorize'

export const router = createBrowserRouter([
  { path: '/login', element: <Login /> },
  { path: '/oauth_login', element: <OAuthLogin /> },
  { path: '/register', element: <Register /> },
  { path: '/forgot-password', element: <ForgotPassword /> },
  { path: '/design-system', element: <DesignSystemPreview /> },
  // 子系统委托 SSO 跳转页（公开，未登录时引导登录后回跳）
  { path: '/sso/authorize', element: <SsoAuthorize /> },
  {
    path: '/',
    element: (
      <RequireAuth>
        <AppLayout />
      </RequireAuth>
    ),
    children: [
      { index: true, element: <Navigate to="/apps" replace /> },
      // 概览页已下线，旧地址重定向到应用市场
      { path: 'dashboard', element: <Navigate to="/apps" replace /> },
      { path: 'apps', element: <AppPlaza /> },
      { path: 'prompts', element: <PromptCenter /> },
      { path: 'prompts/new', element: <PromptEditor /> },
      { path: 'prompts/:promptId/edit', element: <PromptEditor /> },
      { path: 'prompt-market', element: <PromptCenter /> },
      { path: 'skill-market', element: <Skills /> },
      { path: 'account', element: <AccountSettings /> },
      { path: 'users', element: <Users /> },
      { path: 'admin/teams', element: <AdminTeams /> },
      { path: 'admin/integrations', element: <AdminIntegrations /> },
      { path: 'admin/subsystems', element: <Navigate to="/admin/integrations" replace /> },
      { path: 'publications', element: <Publications /> },
      { path: 'team', element: <Teams /> },
      { path: 'team/:id/:section?', element: <TeamManage /> },
      { path: 'team/:teamId/prompt/new', element: <PromptEditor /> },
      { path: 'team/:teamId/prompt/:promptId/edit', element: <PromptEditor /> },
      { path: 'team/:teamId/app/:appId/:section?', element: <AppWorkspace /> },
      { path: 'team/:teamId/app/:appId/chat/:sessionId?', element: <AppChat /> },
      // 我的记忆管理页（当前用户在本应用下的长期记忆，仅删除/清空）
      { path: 'team/:teamId/app/:appId/memory/mine', element: <MyMemoryPage /> },
      // 记忆管理页（个人跨应用：按应用查看本人记忆 + 个人全局记忆维护）
      { path: 'memory', element: <MemoryManagement /> },
      { path: 'team/:teamId/wiki/:wikiId/:section?', element: <WikiDetail /> },
      { path: 'team/:teamId/wiki/:wikiId/document/:documentId/:section?', element: <WikiDocumentDetail /> },
      { path: 'team/:teamId/kg/:graphId/:section?', element: <KnowledgeGraphDetail /> },
      // 团队模板列表页独立路由，与系统模板页（/plugin/templates）互不混用
      { path: 'team/:teamId/plugin/templates', element: <PluginTemplates /> },
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
      { path: '*', element: <Navigate to="/apps" replace /> },
    ],
  },
])
