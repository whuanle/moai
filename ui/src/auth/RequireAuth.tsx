import { useEffect, useState, type ReactNode } from 'react'
import { Navigate, useLocation, useNavigate } from 'react-router'
import { Spin } from 'antd'
import { useAppStore } from '@/store/app'
import { checkToken, refreshUserProfile } from '@/api/auth'

const TOKEN_CHECK_INTERVAL = 60_000

export function RequireAuth({ children }: { children: ReactNode }) {
  const navigate = useNavigate()
  const location = useLocation()
  const accessToken = useAppStore((state) => state.userInfo?.accessToken)
  const mfaPending = useAppStore((state) => state.userInfo?.mfaPending)
  const [checking, setChecking] = useState(true)

  useEffect(() => {
    let active = true

    const runCheck = async (redirectOnFail: boolean, withProfile = false) => {
      let ok = false
      try {
        ok = await checkToken()
      } catch {
        ok = false
      }
      // 进入应用时重取用户信息：登录响应只带 token，isAdmin/isRoot/头像昵称以服务端为准，避免本地持久化快照滞留
      if (ok && withProfile) {
        try {
          await refreshUserProfile()
        } catch {
          // 拉取失败沿用本地快照，不作为登出理由
        }
      }
      if (!ok && redirectOnFail) {
        useAppStore.getState().clearUserInfo()
        if (active) navigate('/login', { replace: true })
      }
      return ok
    }

    runCheck(true, true).finally(() => {
      if (active) setChecking(false)
    })

    const timer = window.setInterval(() => {
      void runCheck(true)
    }, TOKEN_CHECK_INTERVAL)

    return () => {
      active = false
      window.clearInterval(timer)
    }
  }, [navigate])

  if (!accessToken) return <Navigate to="/login" replace />

  // 系统强制两步验证且用户未绑定：只放行个人中心（2FA 绑定分区），其余页面一律重定向
  if (mfaPending && !location.pathname.startsWith('/account')) {
    return <Navigate to="/account/twofa?bind2fa=1" replace />
  }

  if (checking) {
    return (
      <div
        style={{
          display: 'flex',
          justifyContent: 'center',
          alignItems: 'center',
          minHeight: '100vh',
        }}
      >
        <Spin size="large" />
      </div>
    )
  }

  return children
}
