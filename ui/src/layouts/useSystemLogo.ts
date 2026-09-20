import { useEffect } from 'react'
import { useTranslation } from 'react-i18next'
import { getServerInfo } from '@/api/auth'
import { useAppStore } from '@/store/app'
import { resolveStorageUrl } from '@/utils/storage'

/** 前端内置默认 Logo */
export const DEFAULT_LOGO_SRC = '/logo.svg'

/**
 * 解析当前网站 Logo 地址：超级管理员在系统设置上传后为自定义 Logo，否则回退默认 /logo.svg。
 * store 无 serverInfo 时触发一次拉取（登录/注册页也依赖）。
 */
export function useSystemLogoSrc(): string {
  const logoPath = useAppStore((state) => state.serverInfo?.logoPath ?? '')

  useEffect(() => {
    if (!useAppStore.getState().serverInfo) {
      void getServerInfo()
    }
  }, [])

  return resolveStorageUrl(logoPath) || DEFAULT_LOGO_SRC
}

/**
 * 当前网站展示名称：超级管理员在系统设置修改后全局生效，空时回退 i18n 默认名（app.name）。
 * 与 Logo 共用同一条 serverinfo，store 缺失时触发一次拉取。
 */
export function useSystemName(): string {
  const { t } = useTranslation()
  const name = useAppStore((state) => state.serverInfo?.name ?? '')

  useEffect(() => {
    if (!useAppStore.getState().serverInfo) {
      void getServerInfo()
    }
  }, [])

  return name.trim() || t('app.name')
}
