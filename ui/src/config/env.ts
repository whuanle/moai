function getServerUrl(): string {
  const envUrl = import.meta.env.VITE_ServerUrl
  if (envUrl) return String(envUrl)
  return window.location.origin
}

export const Env = {
  serverUrl: getServerUrl(),
}
