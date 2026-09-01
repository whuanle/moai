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

class FilterRequestHandler implements Middleware {
  next: Middleware | undefined

  async execute(
    url: string,
    requestInit: RequestInit,
    requestOptions?: Record<string, RequestOption>,
  ): Promise<Response> {
    if (!this.next) throw new Error('Next middleware is not set')

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
        getAuthorizationToken: async () => token,
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
