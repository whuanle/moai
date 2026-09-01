import { getApiClient } from '@/api/kiota'

export const SettingKeys = {
  oauthAutoRegister: 'oauth_auto_register',
} as const

export async function getSettings() {
  const client = getApiClient()
  return client.api.settings.get()
}

export async function saveSetting(key: string, value: string): Promise<void> {
  const client = getApiClient()
  await client.api.settings.put({ key, value })
}
