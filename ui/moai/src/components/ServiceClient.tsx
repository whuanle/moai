import { createMoAIClient, MoAIClient } from "../apiClient/moAIClient";
import {
  AnonymousAuthenticationProvider,
  BaseBearerTokenAuthenticationProvider,
  AllowedHostsValidator,
  RequestOption,
  ParseNodeFactoryRegistry,
  SerializationWriterFactoryRegistry,
} from "@microsoft/kiota-abstractions";
import {
  FetchRequestAdapter,
  KiotaClientFactory,
  Middleware,
  MiddlewareFactory,
} from "@microsoft/kiota-http-fetchlibrary";

import { EnvOptions } from "../Env";
import {
  JsonParseNodeFactory,
  JsonSerializationWriterFactory,
} from "@microsoft/kiota-serialization-json";
import { FormInstance, message } from "antd";
import { IsTokenExpired } from "../helper/TokenHelper";
import { GetFileMd5 } from "../helper/Md5Helper";
import { PreUploadFileCommandResponse } from "@/apiClient/models";
// import {
//   PreUploadFileCommandResponse,
//   UploadImageType,
// } from "../apiClient/models";

// 中间件请求
class FilterRequestHandler implements Middleware {
  async execute(
    url: string,
    requestInit: RequestInit,
    requestOptions?: Record<string, RequestOption>
  ): Promise<Response> {
    if (!this.next) {
      throw new Error("Next middleware is not set");
    }

    try {
      let response = await this.next.execute(
        url,
        requestInit as RequestInit,
        requestOptions
      );

      if (response.status === 401) {
        if (!url.includes("login")) {
          message.error("登录过期，请重新登录");
          window.location.href = "/login";
        }
      }

      return response;
    } catch (ex) {
      window.location.href = "/login";
      console.log(ex);
      throw ex;
    }
  }
  next: Middleware | undefined;
}

const parseNodeFactoryRegistry = new ParseNodeFactoryRegistry();
parseNodeFactoryRegistry.contentTypeAssociatedFactories.set(
  "application/json",
  new JsonParseNodeFactory()
);

const serializationRegistry = new SerializationWriterFactoryRegistry();
serializationRegistry.contentTypeAssociatedFactories.set(
  "application/json",
  new JsonSerializationWriterFactory()
);

const handlers = MiddlewareFactory.getDefaultMiddlewares();
handlers.unshift(new FilterRequestHandler());

export const GetApiClient = function (): MoAIClient {
  const token = localStorage.getItem("userinfo.accessToken");
  let authProvider;
  if (token) {
    const jwtToken = token;
    authProvider = new BaseBearerTokenAuthenticationProvider({
      getAuthorizationToken: async () => jwtToken,
      getAllowedHostsValidator: () => new AllowedHostsValidator(),
    });
  } else {
    authProvider = new AnonymousAuthenticationProvider();
  }

  const httpClient = KiotaClientFactory.create(undefined, handlers);
  const adapter = new FetchRequestAdapter(
    authProvider,
    parseNodeFactoryRegistry,
    serializationRegistry,
    httpClient
  );
  adapter.baseUrl = EnvOptions.ServerUrl;
  return createMoAIClient(adapter);
};

export const GetAllowApiClient = function (): MoAIClient {
  const authProvider = new AnonymousAuthenticationProvider();
  const adapter = new FetchRequestAdapter(authProvider);
  adapter.baseUrl = EnvOptions.ServerUrl;
  return createMoAIClient(adapter);
};

// 刷新 token
export const RefreshAccessToken = async function (refreshToken: string) {
  let client = GetAllowApiClient();
  return await client.api.account.refresh_token.post({
    refreshToken: refreshToken,
  });
};

// 上传公开类型的文件
export const UploadPublicFile = async (
  client: MoAIClient,
  file: File
): Promise<PreUploadFileCommandResponse> => {
  const md5 = await GetFileMd5(file);
  const preUploadResponse = await client.api.storage.pre_upload_image.post({
    contentType: file.type,
    fileName: file.name,
    mD5: md5,
    fileSize: file.size,
  });

  if (!preUploadResponse) {
    throw new Error("获取预签名URL失败");
  }

  if (preUploadResponse.isExist === true) {
    return preUploadResponse;
  }

  if (!preUploadResponse.uploadUrl) {
    throw new Error("获取预签名URL失败");
  }

  const uploadUrl = preUploadResponse.uploadUrl;
  if (!uploadUrl) {
    throw new Error("获取预签名URL失败");
  }

  // 使用 fetch API 上传到预签名的 S3 URL
  const uploadResponse = await fetch(uploadUrl, {
    method: "PUT",
    body: file,
    headers: {
      "Content-Type": file.type,
      "x-amz-meta-max-file-size": file.size.toString(),
      "Authorization": `Bearer ${localStorage.getItem("userinfo.accessToken")}`,
    },
  });

  if (uploadResponse.status !== 200) {
    console.error("upload file error:");
    console.error(uploadResponse);
    throw new Error(uploadResponse.statusText);
  }

  await client.api.storage.complate_url.post({
    fileId: preUploadResponse.fileId,
    isSuccess: true,
  });

  return preUploadResponse;
};
