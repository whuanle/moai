import { useSystemLogoSrc } from '@/layouts/useSystemLogo'

interface AppLogoProps {
  /** 边长（px），默认 28 */
  size?: number
  alt?: string
  style?: React.CSSProperties
}

/** 全局网站 Logo（侧边栏/登录/注册页共用），上传替换逻辑见 useSystemLogoSrc */
export function AppLogo({ size = 28, alt = 'logo', style }: AppLogoProps) {
  const src = useSystemLogoSrc()
  return <img key={src} src={src} width={size} height={size} alt={alt} style={style} />
}
