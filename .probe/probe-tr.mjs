// 一次性探测脚本（非 E2E）：验证新语义字段与状态码
import http from 'node:http'
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

const T = Number((await api('POST', '/api/team', { token, body: { name: 'probe-' + TS } })).json.value)
const W1 = Number((await api('POST', '/api/wiki', { token, body: { teamId: T, name: 'probe-w1-' + TS } })).json.value)
const W2 = Number((await api('POST', '/api/wiki', { token, body: { teamId: T, name: 'probe-w2-' + TS } })).json.value)

const kFail = await api('POST', '/api/team-resource-key', { token, body: { teamId: T, resourceType: 'wiki', resourceId: String(W2) } })
console.log('probe1 未开MCP建wiki key:', kFail.status, kFail.text.slice(0, 150))

const cfg0 = await api('GET', `/api/wiki/${W1}/mcp-config`, { token })
console.log('probe2 GET mcp-config 未开:', cfg0.status, cfg0.text.slice(0, 200))

const cfg1 = await api('PUT', `/api/wiki/${W1}/mcp-config`, { token, body: { isMcpEnabled: true } })
console.log('probe3 PUT mcp-config 开:', cfg1.status, cfg1.text.slice(0, 300))

const kOk = await api('POST', '/api/team-resource-key', { token, body: { teamId: T, resourceType: 'wiki', resourceId: String(W1) } })
console.log('probe4 开MCP建key(string):', kOk.status, kOk.text.slice(0, 300))
const KEY1 = kOk.json?.key

const rest = await api('POST', `/api/teamres/wiki/${W1}/documents/list`, { token: KEY1, body: { pageNo: 1, pageSize: 5 } })
console.log('probe5 key REST:', rest.status, rest.text.slice(0, 100))

const mcp = await fetch(`${BASE}/api/teamres/wiki/${W1}/mcp`, { method: 'POST', headers: { 'Content-Type': 'application/json', Accept: 'application/json, text/event-stream', Authorization: `Bearer ${KEY1}` }, body: JSON.stringify({ jsonrpc: '2.0', id: 1, method: 'initialize', params: { protocolVersion: '2025-06-18', capabilities: {}, clientInfo: { name: 'probe', version: '0' } } }) })
console.log('probe6 key MCP init:', mcp.status)

const off = await api('PUT', `/api/wiki/${W1}/mcp-config`, { token, body: { isMcpEnabled: false } })
console.log('probe7a 关MCP mcp-config:', off.status, off.text.slice(0, 200))
const restOff = await api('POST', `/api/teamres/wiki/${W1}/documents/list`, { token: KEY1, body: { pageNo: 1, pageSize: 5 } })
console.log('probe7b 关MCP后key REST:', restOff.status, restOff.json?.error?.code ?? restOff.text.slice(0, 100))

const acc = await api('POST', '/api/access-app', { token, body: { teamId: T, name: 'probe-acc-' + TS } })
const acMcp = await fetch(`${BASE}/api/teamres/wiki/${W1}/mcp`, { method: 'POST', headers: { 'Content-Type': 'application/json', Accept: 'application/json, text/event-stream', Authorization: `Bearer ${acc.json.key}` }, body: JSON.stringify({ jsonrpc: '2.0', id: 1, method: 'initialize', params: {} }) })
console.log('probe8 关MCP后moai-ac-MCP:', acMcp.status)

const on2 = await api('PUT', `/api/wiki/${W1}/mcp-config`, { token, body: { isMcpEnabled: true } })
const restOn = await api('POST', `/api/teamres/wiki/${W1}/documents/list`, { token: KEY1, body: { pageNo: 1, pageSize: 5 } })
console.log('probe9 重新开后key REST:', on2.status, restOn.status)

const A = String((await api('POST', '/api/app', { token, body: { teamId: T, name: 'probe-app-' + TS, appType: 'agent', isExternal: true, isAuth: false } })).json.value)
const feat0 = await api('GET', `/api/app/${A}/feature`, { token })
console.log('probe10 GET feature 未开:', feat0.status, feat0.text.slice(0, 200))
const kAppFail = await api('POST', '/api/team-resource-key', { token, body: { teamId: T, resourceType: 'app', resourceId: A } })
console.log('probe11 未开功能建app key:', kAppFail.status, kAppFail.text.slice(0, 150))
const feat1 = await api('PUT', `/api/app/${A}/feature`, { token, body: { isAcpEnabled: true, isA2aEnabled: true } })
console.log('probe12 PUT feature 开:', feat1.status, feat1.text.slice(0, 400))
const kApp = await api('POST', '/api/team-resource-key', { token, body: { teamId: T, resourceType: 'app', resourceId: A } })
console.log('probe13 开功能后建app key:', kApp.status, kApp.text.slice(0, 400))
const KEY_APP = kApp.json?.key
const acpRes = await fetch(`${BASE}/api/teamres/app/${A}/acp`, { method: 'POST', headers: { 'Content-Type': 'application/json', 'x-api-key': KEY_APP }, body: JSON.stringify({ jsonrpc: '2.0', id: 1, method: 'initialize', params: { protocolVersion: 1, clientCapabilities: {} } }) })
console.log('probe14 app key ACP init:', acpRes.status)
const feat2 = await api('PUT', `/api/app/${A}/feature`, { token, body: { isAcpEnabled: false, isA2aEnabled: true } })
console.log('probe15 关ACP feature:', feat2.status, feat2.text.slice(0, 300))
const acpOff = await fetch(`${BASE}/api/teamres/app/${A}/acp`, { method: 'POST', headers: { 'Content-Type': 'application/json', 'x-api-key': KEY_APP }, body: JSON.stringify({ jsonrpc: '2.0', id: 1, method: 'initialize', params: {} }) })
console.log('probe15b 关ACP后app key ACP:', acpOff.status, (await acpOff.text()).slice(0, 120))
const acpOffAc = await fetch(`${BASE}/api/teamres/app/${A}/acp`, { method: 'POST', headers: { 'Content-Type': 'application/json', 'x-api-key': acc.json.key }, body: JSON.stringify({ jsonrpc: '2.0', id: 1, method: 'initialize', params: {} }) })
console.log('probe15c 关ACP后moai-ac- ACP:', acpOffAc.status)
const card = await fetch(`${BASE}/api/teamres/app/${A}/a2a/agent.json`, { headers: { Authorization: `Bearer ${KEY_APP}` } })
console.log('probe15d 关ACP开A2A后资源key A2A agent.json:', card.status)
const G = Number((await api('POST', '/api/knowledge-graph', { token, body: { teamId: T, name: 'probe-kg-' + TS, templateKey: 'blank' } })).json.value)
const kFailG = await api('POST', '/api/team-resource-key', { token, body: { teamId: T, resourceType: 'knowledgeGraph', resourceId: String(G) } })
console.log('probe16 未开MCP建kg key:', kFailG.status, kFailG.text.slice(0, 120))
const kgCfg = await api('PUT', `/api/knowledge-graph/${G}/mcp-config`, { token, body: { isMcpEnabled: true } })
console.log('probe17 PUT kg mcp-config:', kgCfg.status, kgCfg.text.slice(0, 300))
const kG = await api('POST', '/api/team-resource-key', { token, body: { teamId: T, resourceType: 'knowledgeGraph', resourceId: String(G) } })
console.log('probe18 kg key:', kG.status, kG.text.slice(0, 200))

const kNum = await api('POST', '/api/team-resource-key', { token, body: { teamId: T, resourceType: 'wiki', resourceId: W1 } })
console.log('probe19 number resourceId wiki key:', kNum.status, kNum.text.slice(0, 200), '(created:', kNum.json?.created, 'rowId===probe4 rowId:', kNum.json?.teamResourceKeyId === kOk.json?.teamResourceKeyId, ')')

const list = await api('GET', `/api/team-resource-key/list?teamId=${T}`, { token })
console.log('probe20 list:', list.status, JSON.stringify(list.json?.items?.slice(0, 4)).slice(0, 700))
