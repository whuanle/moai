import { useEffect, useState, type ReactNode } from 'react'
import { Navigate, useNavigate } from 'react-router'
import { Spin } from 'antd'
import { useAppStore } from '@/store/app'
import { checkToken } from '@/api/auth'

const TOKEN_CHECK_INTERVAL = 60_000

export function RequireAuth({ children }: { children: ReactNode }) {
  const navigate = useNavigate()
  const accessToken = useAppStore((state) => state.userInfo?.accessToken)
  const [checking, setChecking] = useState(true)

  useEffect(() => {
    let active = true

    const runCheck = async (redirectOnFail: boolean) => {
      let ok = false
      try {
        ok = await checkToken()
      } catch {
        ok = false
      }
      if (!ok && redirectOnFail) {
        useAppStore.getState().clearUserInfo()
        if (active) navigate('/login', { replace: true })
      }
      return ok
    }

    runCheck(true).finally(() => {
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
