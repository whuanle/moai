// 一次性探测：建应用与发布
import crypto from 'node:crypto'
const BASE = 'http://127.0.0.1:5000'
async function api(method, path, { token, body } = {}) {
  const headers = {}
  if (body !== undefined) headers['Content-Type'] = 'application/json'
  if (token) headers['Authorization'] = `Bearer ${token}`
  const res = await fetch(BASE + path, { method, headers, body: body === undefined ? undefined : JSON.stringify(body) })
  const text = await res.text()
  let json = null
  try { json = JSON.parse(text) } catch {}
  return { status: res.status, json, text }
}
const TS = Date.now().toString().slice(-8)
const si = await api('GET', '/api/common/serverinfo')
const key = crypto.createPublicKey({ key: Buffer.from(si.json.rsaPublic, 'base64'), format: 'der', type: 'spki' })
const rsa = (p) => crypto.publicEncrypt({ key, padding: crypto.constants.RSA_PKCS1_PADDING }, Buffer.from(p, 'utf8')).toString('base64')
const login = await api('POST', '/api/auth/login', { body: { userName: 'admin', password: rsa('abcd123456') } })
const token = login.json.accessToken
const T = Number((await api('POST', '/api/team', { token, body: { name: 'probe2-' + TS } })).json.value)
const app = await api('POST', '/api/app', { token, body: { teamId: T, name: 'probe2-app-' + TS, appType: 'agent', isExternal: true, isAuth: false } })
console.log('create app:', app.status, app.text.slice(0, 200))
const A = String(app.json?.value ?? '')
const cfg = await api('PUT', `/api/app/${A}/agent-config`, { token, body: { modelId: '00000000-0000-0000-0000-000000000000', prompt: 'x', wikiIds: [], plugins: [] } })
console.log('agent-config:', cfg.status, cfg.text.slice(0, 200))
const pub = await api('POST', `/api/app/${A}/publish`, { token, body: {} })
console.log('publish:', pub.status, pub.text.slice(0, 300))
