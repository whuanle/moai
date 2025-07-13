import { MoAIClient } from "./apiClient/moAIClient";
import { RefreshAccessToken, GetApiClient, GetAllowApiClient } from "./components/ServiceClient";
import { IsTokenExpired } from "./helper/TokenHelper";
import useAppStore, { ServerInfoModel, UserInfoModel } from "./stateshare/store";

// 加载服务器公共信息
export const RefreshServerInfo = async (client?: MoAIClient) => {
  try {
    if (!client) {
      client = GetAllowApiClient();
    }
    const response = await client.api.common.serverinfo.get();
    if (response) {
      const serverInfo: ServerInfoModel = {
        serviceUrl: response.serviceUrl!,
        publicStoreUrl: response.publicStoreUrl!,
        rsaPublic: response.rsaPublic!
      };
      useAppStore.getState().setServerInfo(serverInfo);
    }
    return response;
  } catch (error) {
    console.error("Error fetching service info:", error);
    throw error;
  }
};

export const GetServiceInfo = async function (): Promise<ServerInfoModel> {
  const store = useAppStore.getState();
  let serverInfo = store.getServerInfo();
  
  if (!serverInfo) {
    await RefreshServerInfo();
    serverInfo = store.getServerInfo();
  }

  if (!serverInfo) {
    throw new Error("Failed to get server info");
  }

  return serverInfo;
};

// 登录后设置用户信息到缓存
export const SetUserInfo = (userInfo: UserInfoModel) => {
  useAppStore.getState().setUserInfo(userInfo);
};

export const GetUserInfo = (): UserInfoModel | null => {
  return useAppStore.getState().getUserInfo();
};

// 检查 accesstoken 是否有效，过期则自动刷新
export const CheckToken = async () => {
  const userInfo = GetUserInfo();
  if (!userInfo || !userInfo.accessToken) {
    return false;
  }

  try {
    if (IsTokenExpired(userInfo.accessToken)) {
      // 使用 refresh token 刷新 access token
      if (userInfo.refreshToken && !IsTokenExpired(userInfo.refreshToken)) {
        var response = await RefreshAccessToken(userInfo.refreshToken);

        // 刷新失败
        if (!response) {
          return false;
        }

        SetUserInfo(response);

        return true;
      }
    } else {
      return true;
    }
  } catch (error) {
    console.error("Error checking token:", error);
    return false;
  }

  return false;
};

export const GetAccessToken = async (): Promise<string | null> => {
  const userInfo = GetUserInfo();
  if (!userInfo || !userInfo.accessToken) {
    return null;
  }

  try {
    if (IsTokenExpired(userInfo.accessToken)) {
      // 使用 refresh token 刷新 access token
      if (userInfo.refreshToken && !IsTokenExpired(userInfo.refreshToken)) {
        var response = await RefreshAccessToken(userInfo.refreshToken);

        // 刷新失败
        if (!response) {
          return null;
        }

        SetUserInfo(response);

        return response.accessToken!;
      }
    } else {
      return userInfo.accessToken;
    }
  } catch (error) {
    console.error("Error checking token:", error);
    return null;
  }

  return null;
};

// 获取用户详细信息
export const GetUserDetailInfo = async () => {
  try {
    const client = GetApiClient();
    const response = await client.api.common.userinfo.get();
    return response;
  } catch (error) {
    console.error("Error fetching user detail info:", error);
    return null;
  }
};