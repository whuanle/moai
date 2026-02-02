function getServerUrl(): string {
  const envUrl = import.meta.env.VITE_ServerUrl;
  if (envUrl) {
    return String(envUrl);
  }
  // 未配置时使用当前页面的 origin（协议+域名+端口）
  return window.location.origin;
}

export const EnvOptions = {
  ServerUrl: getServerUrl()
};
