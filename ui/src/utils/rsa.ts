import JSEncrypt from 'jsencrypt'

export function rsaEncrypt(publicKey: string, data: string): string {
  const encryptor = new JSEncrypt()
  encryptor.setPublicKey(publicKey)
  const encrypted = encryptor.encrypt(data)
  if (!encrypted) throw new Error('Encryption failed')
  return btoa(atob(encrypted))
}
