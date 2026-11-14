import { rsaEncrypt } from '@/utils/rsa'
import { getApiClient } from '@/api/kiota'

function bytesToBase64(bytes: Uint8Array): string {
  let binary = ''
  bytes.forEach((b) => {
    binary += String.fromCharCode(b)
  })
  return btoa(binary)
}

function base64ToBytes(value: string): Uint8Array {
  const binary = atob(value)
  const bytes = new Uint8Array(binary.length)
  for (let i = 0; i < binary.length; i += 1) {
    bytes[i] = binary.charCodeAt(i)
  }
  return bytes
}

/**
 * 密钥回显（RSA+AES 信封，端到端加密传输）：
 * 前端随机生成 AES-256-GCM key，经平台 RSA 公钥（PKCS1）加密随请求上送；
 * 服务端以该 key 的 GCM 密文返回原文，本地解密展示——服务端不落信封 key，列表/传输全程无明文.
 */
export async function revealSubsysAppSecret(id: string): Promise<string> {
  const client = getApiClient()
  const serverInfo = await client.api.common.serverinfo.get()
  const rsaPublic = serverInfo?.rsaPublic ?? ''
  if (!rsaPublic) {
    throw new Error('missing rsa public key')
  }

  const envelopeKey = await crypto.subtle.generateKey({ name: 'AES-GCM', length: 256 }, true, ['encrypt', 'decrypt'])
  const rawKey = new Uint8Array(await crypto.subtle.exportKey('raw', envelopeKey))
  const encryptedKey = rsaEncrypt(rsaPublic, bytesToBase64(rawKey))

  const res = await client.api.subsystem.byId(id).revealSecret.post({ encryptedKey })
  const iv = base64ToBytes(res?.iv ?? '')
  const payload = base64ToBytes(res?.data ?? '')
  const plain = await crypto.subtle.decrypt({ name: 'AES-GCM', iv }, envelopeKey, payload)
  return new TextDecoder().decode(plain)
}
