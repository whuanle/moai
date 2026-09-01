import { getApiClient } from '@/api/kiota'
import { getServerInfo } from '@/api/auth'
import { rsaEncrypt } from '@/utils/rsa'
import { uploadImageWithKey } from '@/utils/storage'

export interface AccountBoundItem {
  oAuthId?: string | null
  name?: string | null
  provider?: string | null
  iconUrl?: string | null
  createTime?: string | null
}

export async function getBoundAccounts(): Promise<AccountBoundItem[]> {
  const client = getApiClient()
  const res = await client.api.account.bound_accounts.get()
  return res?.items ?? []
}

export async function updateUserInfo(payload: { nickName?: string; phone?: string }): Promise<void> {
  const client = getApiClient()
  await client.api.account.update_userinfo.post(payload)
}

export async function resetPassword(oldPassword: string, newPassword: string): Promise<void> {
  const info = await getServerInfo()
  const client = getApiClient()
  await client.api.account.reset_password.post({
    oldPassword: rsaEncrypt(info.rsaPublic, oldPassword),
    newPassword: rsaEncrypt(info.rsaPublic, newPassword),
  })
}

export async function uploadAvatar(file: File): Promise<string> {
  const client = getApiClient()
  const { objectKey, url } = await uploadImageWithKey(file)
  await client.api.account.avatar.post({ objectKey })
  return url
}

export async function unbindProvider(providerId: string): Promise<void> {
  const client = getApiClient()
  await client.api.account.unbind_account.post({ providerId })
}

export async function oauthBindByCode(payload: { code: string; oAuthId: string }): Promise<void> {
  const client = getApiClient()
  await client.api.account.oauth_bind.post({ code: payload.code, oAuthId: payload.oAuthId })
}
