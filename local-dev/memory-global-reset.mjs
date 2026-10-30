// 一次性运维脚本：重置平台记忆状态（root 清空全部记忆（应用+全局）+ 关闭开关 + 清空统一模型设置）
import crypto from 'node:crypto'

const BASE = process.argv[2] ?? 'http://127.0.0.1:5000'
const si = await fetch(BASE + '/api/common/serverinfo').then((r) => r.json())
const key = crypto.createPublicKey({ key: Buffer.from(si.rsaPublic, 'base64'), format: 'der', type: 'spki' })
const rsa = (plain) => crypto.publicEncrypt({ key, padding: crypto.constants.RSA_PKCS1_PADDING }, Buffer.from(plain, 'utf8')).toString('base64')

const login = await fetch(BASE + '/api/auth/login', {
  method: 'POST', headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({ userName: 'admin', password: rsa('abcd123456') }),
}).then((r) => r.json())

const call = (method, path, body) => fetch(BASE + path, {
  method, headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${login.accessToken}` },
  body: body === undefined ? undefined : JSON.stringify(body),
})

console.log('clear all memories:', (await call('DELETE', '/api/memory/all')).status)
console.log('reset model:', (await call('PUT', '/api/settings', { key: 'MEMORY_EMBEDDING_MODEL', value: '' })).status)
console.log('reset dimensions:', (await call('PUT', '/api/settings', { key: 'MEMORY_EMBEDDING_DIMENSIONS', value: '1024' })).status)
console.log('reset enabled:', (await call('PUT', '/api/settings', { key: 'MEMORY_ENABLED', value: 'false' })).status)
