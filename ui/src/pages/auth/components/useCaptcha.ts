import { useCallback, useState } from 'react'
import { fetchCaptcha, type CaptchaImage } from '@/api/auth'

/** 图形验证码状态管理：按需拉取/换一张/清空（登录/注册/找回密码页共用） */
export function useCaptcha() {
  const [captcha, setCaptcha] = useState<CaptchaImage | null>(null)

  /** 拉取新验证码（首次显示或换一张或旧码失效后调用） */
  const refreshCaptcha = useCallback(async () => {
    try {
      setCaptcha(await fetchCaptcha())
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }, [])

  const clearCaptcha = useCallback(() => setCaptcha(null), [])

  return { captcha, refreshCaptcha, clearCaptcha }
}
