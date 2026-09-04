import '@testing-library/jest-dom'

// Node >= 25 暴露实验性 localStorage（Web Storage，需 --localstorage-file 路径），会遮蔽 jsdom 的 window.localStorage，
// 导致 zustand persist 初始化时 localStorage.getItem 抛错。此处用内存实现为 globalThis 与 window 挂载标准 localStorage。
const memoryStorage = (() => {
  let store: Record<string, string> = {}
  return {
    getItem: (k: string) => (k in store ? store[k] : null),
    setItem: (k: string, v: string) => { store[k] = String(v) },
    removeItem: (k: string) => { delete store[k] },
    clear: () => { store = {} },
    key: (i: number) => Object.keys(store)[i] ?? null,
    get length() { return Object.keys(store).length },
  }
})()
// @ts-expect-error Node 实验性 Web Storage 全局
delete globalThis.localStorage
Object.defineProperty(globalThis, 'localStorage', { writable: true, configurable: true, value: memoryStorage })
Object.defineProperty(window, 'localStorage', { writable: true, configurable: true, value: memoryStorage })

Object.defineProperty(window, 'matchMedia', {
  writable: true,
  value: (query: string) => ({
    matches: false,
    media: query,
    onchange: null,
    addListener: () => {},
    removeListener: () => {},
    addEventListener: () => {},
    removeEventListener: () => {},
    dispatchEvent: () => false,
  }),
})
