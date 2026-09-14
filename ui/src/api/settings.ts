import { getApiClient } from '@/api/kiota'

export const SettingKeys = {
  kgEnabled: 'KG_ENABLED',
  kgUri: 'KG_URI',
  kgUsername: 'KG_USERNAME',
  kgPassword: 'KG_PASSWORD',
  kgDialect: 'KG_DIALECT',
} as const

export async function getSettings() {
  const client = getApiClient()
  return client.api.settings.get()
}

export async function saveSetting(key: string, value: string): Promise<void> {
  const client = getApiClient()
  await client.api.settings.put({ key, value })
}
