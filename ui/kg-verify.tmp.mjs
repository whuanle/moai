import crypto from 'node:crypto'
import { chromium } from 'playwright'

const api = async (method, path, { token, body } = {}) => {
  const res = await fetch(`http://127.0.0.1:5000${path}`, {
    method,
    headers: { 'Content-Type': 'application/json', ...(token ? { Authorization: `Bearer ${token}` } : {}) },
    body: body ? JSON.stringify(body) : undefined,
  })
  let json = null
  try { json = await res.json() } catch {}
  return { status: res.status, json }
}
const rsa = (plain, keyB64) => {
  const key = crypto.createPublicKey({ key: Buffer.from(keyB64, 'base64'), format: 'der', type: 'spki' })
  return crypto.publicEncrypt({ key, padding: crypto.constants.RSA_PKCS1_PADDING }, Buffer.from(plain, 'utf8')).toString('base64')
}
const si = await api('GET', '/api/common/serverinfo')
const login = await api('POST', '/api/auth/login', { body: { userName: 'admin', password: rsa('abcd123456', si.json.rsaPublic) } })
const token = login.json?.accessToken
console.log('token ok:', Boolean(token))

const browser = await chromium.launch({ channel: 'msedge' })
const page = await browser.newPage({ viewport: { width: 1600, height: 900 } })

// 记忆坑：登录页 Playwright 填表失效 → 用原生事件赋值 + input/change；随后 SPA 导航
await page.goto('http://127.0.0.1:4000/auth/login', { waitUntil: 'networkidle' })
await page.evaluate((tk) => {
  const el = document.querySelector('input')
  return Boolean(el)
}, token)
// 直接走 SPA 登录：输入原生赋值
const user = page.locator('input').first()
await user.click()
await page.evaluate(() => {
  const inputs = document.querySelectorAll('input')
  const setVal = (el, v) => {
    const setter = Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, 'value').set
    setter.call(el, v)
    el.dispatchEvent(new Event('input', { bubbles: true }))
  }
  setVal(inputs[0], 'admin')
  setVal(inputs[1], 'abcd123456')
})
await page.screenshot({ path: '/tmp/kg-shots/login-filled.png' })
await page.getByRole('button', { name: /登录|登 录|Login/i }).click()
await page.waitForTimeout(2500)
console.log('after login url:', page.url())
console.log('token in storage:', await page.evaluate(() => Object.keys(window.localStorage).join(',')))

// SPA 内部导航到知识图谱图览
await page.evaluate(() => { window.history.pushState({}, '', '/team/1/kg/104'); window.dispatchEvent(new PopStateEvent('popstate')) })
await page.waitForTimeout(4000)
console.log('canvas url:', page.url())
await page.screenshot({ path: '/tmp/kg-shots/canvas.png', fullPage: false })

await page.evaluate(() => { window.history.pushState({}, '', '/team/1/kg/104/maintenance?step=schema'); window.dispatchEvent(new PopStateEvent('popstate')) })
await page.waitForTimeout(2500)
await page.screenshot({ path: '/tmp/kg-shots/maintenance-schema.png' })

await page.evaluate(() => { window.history.pushState({}, '', '/team/1/kg/104/maintenance?step=entities'); window.dispatchEvent(new PopStateEvent('popstate')) })
await page.waitForTimeout(2500)
await page.screenshot({ path: '/tmp/kg-shots/maintenance-entities.png' })

await page.evaluate(() => { window.history.pushState({}, '', '/team/1/kg/104/relations'); window.dispatchEvent(new PopStateEvent('popstate')) })
await page.waitForTimeout(2500)
await page.screenshot({ path: '/tmp/kg-shots/legacy-redirect.png' })

await browser.close()
console.log('done')
