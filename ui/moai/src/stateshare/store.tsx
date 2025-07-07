import { create } from 'zustand'
import { UserStateInfo } from '../apiClient/models'

export interface ServerInfoModel {
  /**
   * 公共存储地址，静态资源时可直接访问.
   */
  publicStoreUrl: string;
  /**
   * RSA 公钥，用于加密密码等信息传输到服务器.
   */
  rsaPublic: string;
  /**
   * 系统访问地址.
   */
  serviceUrl: string;
}

export interface UserInfoModel {
  /**
   * 访问令牌.
   */
  accessToken?: string | null;
  /**
   * 过期时间（秒）.
   */
  expiresIn?: string | null;
  /**
   * 刷新令牌.
   */
  refreshToken?: string | null;
  /**
   * 令牌类型.
   */
  tokenType?: string | null;
  /**
   * 用户ID.
   */
  userId?: number | null;
  /**
   * 用户名.
   */
  userName?: string | null;
}

interface AppState {
  currentTeam: any
  serverInfo: ServerInfoModel | null
  userInfo: UserInfoModel | null
  userDetailInfo: UserStateInfo | null
  
  // Server info actions
  setServerInfo: (serverInfo: ServerInfoModel) => void
  getServerInfo: () => ServerInfoModel | null
  clearServerInfo: () => void
  
  // User info actions
  setUserInfo: (userInfo: UserInfoModel) => void
  getUserInfo: () => UserInfoModel | null
  clearUserInfo: () => void
  
  // User detail info actions
  setUserDetailInfo: (userDetailInfo: UserStateInfo) => void
  getUserDetailInfo: () => UserStateInfo | null
  clearUserDetailInfo: () => void
}

const useAppStore = create<AppState>((set, get) => ({
  currentTeam: null,
  serverInfo: null,
  userInfo: null,
  userDetailInfo: null,
  
  // Server info actions
  setServerInfo: (serverInfo: ServerInfoModel) => {
    // Save to localStorage
    localStorage.setItem("serverinfo.serviceUrl", serverInfo.serviceUrl);
    localStorage.setItem("serverinfo.publicStoreUrl", serverInfo.publicStoreUrl);
    localStorage.setItem("serverinfo.rsaPublic", serverInfo.rsaPublic);
    
    // Update store state
    set({ serverInfo });
  },
  
  getServerInfo: () => {
    const state = get();
    if (state.serverInfo) {
      return state.serverInfo;
    }
    
    // Try to load from localStorage
    const serviceUrl = localStorage.getItem("serverinfo.serviceUrl");
    const publicStoreUrl = localStorage.getItem("serverinfo.publicStoreUrl");
    const rsaPublic = localStorage.getItem("serverinfo.rsaPublic");
    
    if (serviceUrl && publicStoreUrl && rsaPublic) {
      const serverInfo: ServerInfoModel = {
        serviceUrl,
        publicStoreUrl,
        rsaPublic
      };
      set({ serverInfo });
      return serverInfo;
    }
    
    return null;
  },
  
  clearServerInfo: () => {
    localStorage.removeItem("serverinfo.serviceUrl");
    localStorage.removeItem("serverinfo.publicStoreUrl");
    localStorage.removeItem("serverinfo.rsaPublic");
    set({ serverInfo: null });
  },
  
  // User info actions
  setUserInfo: (userInfo: UserInfoModel) => {
    // Save to localStorage
    if (userInfo.accessToken) localStorage.setItem("userinfo.accessToken", userInfo.accessToken);
    if (userInfo.expiresIn) localStorage.setItem("userinfo.expiresIn", userInfo.expiresIn);
    if (userInfo.refreshToken) localStorage.setItem("userinfo.refreshToken", userInfo.refreshToken);
    if (userInfo.tokenType) localStorage.setItem("userinfo.tokenType", userInfo.tokenType);
    if (userInfo.userId) localStorage.setItem("userinfo.userId", userInfo.userId.toString());
    if (userInfo.userName) localStorage.setItem("userinfo.userName", userInfo.userName);
    
    // Update store state
    set({ userInfo });
  },
  
  getUserInfo: () => {
    const state = get();
    if (state.userInfo) {
      return state.userInfo;
    }
    
    // Try to load from localStorage
    const accessToken = localStorage.getItem("userinfo.accessToken");
    const expiresIn = localStorage.getItem("userinfo.expiresIn");
    const refreshToken = localStorage.getItem("userinfo.refreshToken");
    const tokenType = localStorage.getItem("userinfo.tokenType");
    const userIdStr = localStorage.getItem("userinfo.userId");
    const userName = localStorage.getItem("userinfo.userName");
    
    if (accessToken || refreshToken) {
      const userInfo: UserInfoModel = {
        accessToken,
        expiresIn,
        refreshToken,
        tokenType,
        userId: userIdStr ? Number(userIdStr) : null,
        userName
      };
      set({ userInfo });
      return userInfo;
    }
    
    return null;
  },
  
  clearUserInfo: () => {
    localStorage.removeItem("userinfo.accessToken");
    localStorage.removeItem("userinfo.expiresIn");
    localStorage.removeItem("userinfo.refreshToken");
    localStorage.removeItem("userinfo.tokenType");
    localStorage.removeItem("userinfo.userId");
    localStorage.removeItem("userinfo.userName");
    set({ userInfo: null });
  },
  
  // User detail info actions (no browser storage)
  setUserDetailInfo: (userDetailInfo: UserStateInfo) => {
    set({ userDetailInfo });
  },
  
  getUserDetailInfo: () => {
    const state = get();
    return state.userDetailInfo;
  },
  
  clearUserDetailInfo: () => {
    set({ userDetailInfo: null });
  },
  

}))

export default useAppStore
