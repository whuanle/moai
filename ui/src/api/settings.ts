import { getApiClient } from '@/api/kiota'

export const SettingKeys = {
  neo4jEnabled: 'OPEN_NEO4J',
  neo4jUri: 'NEO4J_URI',
  neo4jUsername: 'NEO4J_USERNAME',
  neo4jPassword: 'NEO4J_PASSWORD',
} as const

export async function getSettings() {
  const client = getApiClient()
  return client.api.settings.get()
}

export async function saveSetting(key: string, value: string): Promise<void> {
  const client = getApiClient()
  await client.api.settings.put({ key, value })
}
