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
import { feedback, isNetworkError } from '@/design-system/components/Feedback'

class FilterRequestHandler implements Middleware {
  next: Middleware | undefined

  async execute(
    url: string,
    requestInit: RequestInit,
    requestOptions?: Record<string, RequestOption>,
  ): Promise<Response> {
    if (!this.next) throw new Error('Next middleware is not set')

    try {
      const response = await this.next.execute(url, requestInit, requestOptions)
      if (response.status === 401 && !url.includes('login')) {
        useAppStore.getState().clearUserInfo()
        window.location.href = '/login'
        return response
      }
      if (!response.ok && response.status !== 401) {
        feedback.handleError(response)
      }
      return response
    } catch (error) {
      feedback.handleError(error)
      if (!isNetworkError(error)) {
        useAppStore.getState().clearUserInfo()
      }
      console.error(error)
      throw error
    }
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

const middleware = MiddlewareFactory.getDefaultMiddlewares()
middleware.unshift(new FilterRequestHandler())

function buildAdapter(): FetchRequestAdapter {
  const token = useAppStore.getState().userInfo?.accessToken
  const authProvider = token
    ? new BaseBearerTokenAuthenticationProvider({
        getAuthorizationToken: async () => token,
        getAllowedHostsValidator: () => new AllowedHostsValidator(),
      })
    : new AnonymousAuthenticationProvider()

  const httpClient = KiotaClientFactory.create(undefined, middleware)
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
  const adapter = new FetchRequestAdapter(new AnonymousAuthenticationProvider())
  adapter.baseUrl = Env.serverUrl
  return createMoAIClient(adapter)
}
