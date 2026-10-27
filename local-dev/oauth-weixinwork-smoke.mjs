// 企业微信第三方登录冒烟（场景 @OC-S26 ~ @OC-S28、@AUTH-S13/@AUTH-S25 的 URL 拼接部分）
// 覆盖：企业微信连接器创建（agentId 必填校验/回显）+ 固定授权地址 + 登录页跳转 URL 拼接
//       （login_type=CorpApp/appid=corpid/agentid/state/redirect_uri）+ 更新 agentId 同步 + 删除清理。
// 用法：node local-dev/oauth-weixinwork-smoke.mjs [baseUrl]（默认 http://127.0.0.1:5000）
// 前置：后端运行中；种子账号 admin / abcd123456（root）。假凭证即可，不发起真实企业微信调用；脚本结束删除测试连接。
import crypto from 'node:crypto'

const BASE = process.env.APP_BASE ?? process.argv[2] ?? 'http://127.0.0.1:5000'
let PASS = 0, FAIL = 0
const check = (name, cond, detail = '') => {
  if (cond) { PASS++; console.log(`PASS | ${name}`) }
  else { FAIL++; console.log(`FAIL | ${name} ${detail ? '— ' + detail : ''}`) }
}

let RSA_KEY = ''
const rsa = (plain) => {
  const key = crypto.createPublicKey({ key: Buffer.from(RSA_KEY, 'base64'), format: 'der', type: 'spki' })
  return crypto.publicEncrypt({ key, padding: crypto.constants.RSA_PKCS1_PADDING }, Buffer.from(plain, 'utf-8')).toString('base64')
}
async function api(method, path, { token, body } = {}) {
  const headers = {}
  if (body !== undefined) headers['Content-Type'] = 'application/json'
  if (token) headers['Authorization'] = `Bearer ${token}`
  const res = await fetch(BASE + path, { method, headers, body: body === undefined ? undefined : JSON.stringify(body) })
  const text = await res.text()
  let json = null
  try { json = JSON.parse(text) } catch { /* 非 JSON */ }
  return { status: res.status, json, text }
}

async function main() {
  const si = await api('GET', '/api/common/serverinfo')
  RSA_KEY = si.json.rsaPublic
  const l = await api('POST', '/api/auth/login', { body: { userName: 'admin', password: rsa('abcd123456') } })
  const token = l.json.accessToken
  check('OC-00 admin 登录', Boolean(token))

  const name = `ww-smoke-${Date.now()}`

  // @OC-S27 企业微信缺 AgentId 被拒
  const bad = await api('POST', '/api/oauthconnect/connections', { token, body: {
    name, provider: 'weixinWork', key: 'wwfake123456', secret: 'fake-secret', iconUrl: 'x', agentId: '',
  } })
  check('OC-S27 缺 AgentId 创建被拒 400', bad.status === 400, `got ${bad.status} ${bad.text.slice(0, 120)}`)

  // @OC-S26 创建企业微信连接器
  const ok = await api('POST', '/api/oauthconnect/connections', { token, body: {
    name, provider: 'weixinWork', key: 'wwfake123456', secret: 'fake-secret', iconUrl: 'x', agentId: '1000002',
  } })
  check('OC-S26 创建企业微信连接器成功', ok.status === 200, `got ${ok.status} ${ok.text.slice(0, 200)}`)

  const list = await api('GET', '/api/oauthconnect/connections', { token })
  const item = (list.json.items ?? []).find((x) => x.name === name)
  check('OC-S26 agentId 回显', item?.agentId === '1000002', JSON.stringify(item ?? {}).slice(0, 200))
  check('OC-S3 列表返回回调地址 redirectUri', typeof list.json.redirectUri === 'string' && new URL(list.json.redirectUri).pathname === '/oauth_login', list.json.redirectUri)
  check('OC-S26 authorizeUrl 固定为 wwlogin', item?.authorizeUrl === 'https://login.work.weixin.qq.com/wwlogin/sso/login', item?.authorizeUrl)

  // @AUTH-S13/@AUTH-S25 登录页跳转 URL 拼接（企业微信参数）
  const prov = await api('GET', '/api/auth/oauth_prividers', { token })
  const pv = (prov.json.items ?? []).find((x) => x.oAuthId === item.id)
  check('AUTH-S13 登录页含企业微信提供方', Boolean(pv), JSON.stringify(prov.json.items ?? []).slice(0, 200))
  const url = new URL(pv?.redirectUrl ?? 'http://invalid')
  check('AUTH-S13 URL 端点为 wwlogin/sso/login', url.origin + url.pathname === 'https://login.work.weixin.qq.com/wwlogin/sso/login', pv?.redirectUrl)
  check('AUTH-S25 login_type=CorpApp', url.searchParams.get('login_type') === 'CorpApp')
  check('AUTH-S25 appid=corpid', url.searchParams.get('appid') === 'wwfake123456')
  check('AUTH-S25 agentid=配置的 AgentId', url.searchParams.get('agentid') === '1000002')
  check('AUTH-S13 state=oauthId', url.searchParams.get('state') === item.id)
  const redirect = url.searchParams.get('redirect_uri') ?? ''
  check('AUTH-S13 redirect_uri 指向 WebUI /oauth_login', redirect !== '' && new URL(redirect).pathname === '/oauth_login', redirect)

  // @AUTH-S14 回调地址跟随登录页来源（内网穿透场景），非法协议拒绝
  const provT = await api('GET', '/api/auth/oauth_prividers?redirectUrl=' + encodeURIComponent('https://4000.local.whuanle.cn'), { token })
  const redirectT = new URL(new URL(provT.json.items[0].redirectUrl).searchParams.get('redirect_uri'))
  check('AUTH-S14 redirectUrl 跟随登录页来源', redirectT.origin === 'https://4000.local.whuanle.cn' && redirectT.pathname === '/oauth_login', provT.json.items[0].redirectUrl)
  const provBad = await api('GET', '/api/auth/oauth_prividers?redirectUrl=' + encodeURIComponent('ftp://evil.com'), { token })
  check('AUTH-S14 非 http/https 的 redirectUrl 被拒 400', provBad.status === 400, `got ${provBad.status}`)

  // @OC-S28 更新企业微信连接器（改 agentId 即时生效）
  const upd = await api('PUT', `/api/oauthconnect/connections/${item.id}`, { token, body: {
    name, provider: 'weixinWork', key: 'wwfake123456', iconUrl: 'x', agentId: '1000009', secret: '',
  } })
  check('OC-S28 更新成功（secret 留空保持不变）', upd.status === 200, `got ${upd.status} ${upd.text.slice(0, 200)}`)
  const list2 = await api('GET', '/api/oauthconnect/connections', { token })
  const item2 = (list2.json.items ?? []).find((x) => x.name === name)
  check('OC-S28 更新后 agentId 回显', item2?.agentId === '1000009', item2?.agentId)
  const prov2 = await api('GET', '/api/auth/oauth_prividers', { token })
  const pv2 = (prov2.json.items ?? []).find((x) => x.oAuthId === item.id)
  check('OC-S28 跳转 URL agentid 同步更新', new URL(pv2?.redirectUrl).searchParams.get('agentid') === '1000009')

  // 清理
  const del = await api('DELETE', `/api/oauthconnect/connections/${item.id}`, { token })
  check('OC-S16 删除成功', del.status === 200)
  const list3 = await api('GET', '/api/oauthconnect/connections', { token })
  check('OC-S16 列表已无该连接', !(list3.json.items ?? []).some((x) => x.name === name))

  console.log(`\nRESULT: PASS=${PASS} FAIL=${FAIL}`)
  process.exit(FAIL > 0 ? 1 : 0)
}

await main()
