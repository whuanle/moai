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

const browser = await chromium.launch({ channel: 'msedge' })
const page = await browser.newPage({ viewport: { width: 1600, height: 900 } })
await page.goto('http://127.0.0.1:4000/auth/login', { waitUntil: 'networkidle' })
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
await page.getByRole('button', { name: /登录|登 录|Login/i }).click()
await page.waitForTimeout(2000)
await page.evaluate(() => { window.history.pushState({}, '', '/team/1/kg/104'); window.dispatchEvent(new PopStateEvent('popstate')) })
await page.waitForTimeout(4000)

const rect = await page.evaluate(() => {
  const c = [...document.querySelectorAll('div')].find((d) => d.querySelector('canvas') && d.querySelector('button'))
  return c ? { w: Math.round(c.getBoundingClientRect().width), h: Math.round(c.getBoundingClientRect().height) } : null
})
console.log('canvas rect before fullscreen:', JSON.stringify(rect))

const toolbarBtn = page.locator('button').filter({ has: page.locator('svg[data-icon="fullscreen"]') })
await toolbarBtn.first().click()
await page.waitForTimeout(1500)
const fsState = await page.evaluate(() => ({
  fullscreen: Boolean(document.fullscreenElement),
}))
console.log('fullscreen state:', JSON.stringify(fsState))
await page.screenshot({ path: 'F:/tmp/kg-shots/canvas-fullscreen.png' })
await toolbarBtn.first().click()
await page.waitForTimeout(800)
console.log('after exit fullscreen:', await page.evaluate(() => Boolean(document.fullscreenElement)))
await browser.close()
console.log('done')
