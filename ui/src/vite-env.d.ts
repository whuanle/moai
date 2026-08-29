/// <reference types="vite/client" />

interface ImportMetaEnv {
  readonly VITE_ServerUrl?: string
}

interface ImportMeta {
  readonly env: ImportMetaEnv
}
