import { createAiPluginClient, type AiPluginClient } from '@/api/aiplugin-client/aiPluginClient'
import { createMoAIClient, type MoAIClient } from '@/api/client/moAIClient'
import {
  AllowedHostsValidator,
  AnonymousAuthenticationProvider,
  BaseBearerTokenAuthenticationProvider,
  type ParseNodeFactoryRegistry,
  ParseNodeFactoryRegistry as ParseNodeFactoryRegistryImpl,
  type RequestOption,
  SerializationWriterFactoryRegistry as SerializationWriterFactoryRegistryImpl,
  type SerializationWriterFactoryRegistry,
} from '@microsoft/kiota-abstractions'
import {
  FetchRequestAdapter,
  KiotaClientFactory,
  type Middleware,
  MiddlewareFactory,
} from '@microsoft/kiota-http-fetchlibrary'
import { JsonParseNodeFactory, JsonSerializationWriterFactory } from '@microsoft/kiota-serialization-json'
import { Env } from '@/config/env'
import { useAppStore } from '@/store/app'
import { feedback, isNetworkError, parseApiErrorResponse } from '@/design-system/components/Feedback'
import { trySessionRefresh } from '@/api/sessionRefresh'

/** 登录/续期自身返回的 401 不参与续期重试（否则递归） */
function isAuthEndpoint(url: string): boolean {
  return url.includes('login') || url.includes('refresh_token')
}

class FilterRequestHandler implements Middleware {
  next: Middleware | undefined

  async execute(
    url: string,
    requestInit: RequestInit,
    requestOptions?: Record<string, RequestOption>,
  ): Promise<Response> {
    if (!this.next) throw new Error('Next middleware is not set')

    // 业务数据 GET 禁用浏览器启发式缓存（否则保存后重新加载可能拿到旧响应）
    const headers = new Headers(requestInit.headers)
    if ((requestInit.method ?? 'GET') === 'GET') {
      headers.set('Cache-Control', 'no-cache')
      headers.set('Pragma', 'no-cache')
    }
    requestInit = { ...requestInit, headers }

    let response: Response
    try {
      response = await this.next.execute(url, requestInit, requestOptions)
    } catch (error) {
      feedback.handleError(error)
      if (!isNetworkError(error)) {
        useAppStore.getState().clearUserInfo()
      }
      console.error('[api] request error:', error)
      throw error
    }

    // 401 先静默续期并重试一次：休眠唤醒、时钟偏差、令牌刚过期等场景不应直接把用户踢回登录页。
    // 续期本身失败（refresh_token 过期/用户被禁用/密钥轮换）才走清态跳转。
    if (response.status === 401 && !isAuthEndpoint(url)) {
      let refreshed = false
      try {
        refreshed = await trySessionRefresh()
      } catch {
        refreshed = false
      }
      if (refreshed) {
        try {
          // adapter 只在首次发送前调用一次 authenticateRequest，重试要用续期后的 token 覆盖鉴权头
          const retryHeaders = new Headers(requestInit.headers)
          const newToken = useAppStore.getState().userInfo?.accessToken
          if (newToken) {
            retryHeaders.set('Authorization', `Bearer ${newToken}`)
          }
          response = await this.next.execute(url, { ...requestInit, headers: retryHeaders }, requestOptions)
        } catch (error) {
          feedback.handleError(error)
          console.error('[api] retry request error:', error)
          throw error
        }
      }
    }

    if (response.status === 401 && !url.includes('login')) {
      useAppStore.getState().clearUserInfo()
      window.location.href = '/login'
      return response
    }

    if (!response.ok) {
      const error = await parseApiErrorResponse(response)
      feedback.handleError(error)
      console.error('[api] response error:', error)
      throw error
    }

    return response
  }
}

const parseNodeFactoryRegistry: ParseNodeFactoryRegistry = new ParseNodeFactoryRegistryImpl()
parseNodeFactoryRegistry.contentTypeAssociatedFactories.set('application/json', new JsonParseNodeFactory())

const serializationWriterFactoryRegistry: SerializationWriterFactoryRegistry =
  new SerializationWriterFactoryRegistryImpl()
serializationWriterFactoryRegistry.contentTypeAssociatedFactories.set(
  'application/json',
  new JsonSerializationWriterFactory(),
)

function createMiddleware(): Middleware[] {
  const middleware = MiddlewareFactory.getDefaultMiddlewares()
  middleware.unshift(new FilterRequestHandler())
  return middleware
}

function buildAdapter(): FetchRequestAdapter {
  const token = useAppStore.getState().userInfo?.accessToken
  const authProvider = token
    ? new BaseBearerTokenAuthenticationProvider({
        // 每次请求现取 store 里的 token：静默续期/换号后旧 adapter 也能带上新 token（401 重试依赖此行为）
        getAuthorizationToken: async () => useAppStore.getState().userInfo?.accessToken ?? '',
        getAllowedHostsValidator: () => new AllowedHostsValidator(),
      })
    : new AnonymousAuthenticationProvider()

  const httpClient = KiotaClientFactory.create(undefined, createMiddleware())
  const adapter = new FetchRequestAdapter(
    authProvider,
    parseNodeFactoryRegistry,
    serializationWriterFactoryRegistry,
    httpClient,
  )
  adapter.baseUrl = Env.serverUrl
  return adapter
}

export function getApiClient(): MoAIClient {
  return createMoAIClient(buildAdapter())
}

/** 插件管理客户端（独立生成，用于 /api/ai/plugin/manage 端点）.
 * @returns 返回 {AiPluginClient}.
 */
export function getAiPluginClient(): AiPluginClient {
  return createAiPluginClient(buildAdapter())
}

/** 为业务封装层提供同一鉴权/中间件配置的 Kiota adapter，用于新 API 尚未进入生成客户端时的强类型请求封装. */
export function createAiPluginRequestAdapter(): FetchRequestAdapter {
  return buildAdapter()
}

export function getAnonymousClient(): MoAIClient {
  const httpClient = KiotaClientFactory.create(undefined, createMiddleware())
  const adapter = new FetchRequestAdapter(
    new AnonymousAuthenticationProvider(),
    parseNodeFactoryRegistry,
    serializationWriterFactoryRegistry,
    httpClient,
  )
  adapter.baseUrl = Env.serverUrl
  return createMoAIClient(adapter)
}
