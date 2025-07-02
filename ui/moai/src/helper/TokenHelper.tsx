import { jwtDecode } from "jwt-decode";

/*
interface Result$0 {
  sub: string;
  nameid: string;
  name: string;
  nickname: string;
  email: string;
  jti: string;
  token_type: string;
  nbf: number;
  exp: number;
  iat: number;
  iss: string;
  aud: string;
};
{
  "sub": "01ddd3b4-769d-422b-be96-7ca63bb9dcc6",
  "nameid": "01ddd3b4-769d-422b-be96-7ca63bb9dcc6",
  "name": "wwwwww",
  "nickname": "测试",
  "email": "111@qq.com",
  "jti": "f6914ca9-f84d-4f0a-9727-1b271968806d",
  "token_type": "access",
  "nbf": 1745756311,
  "exp": 1748348311,
  "iat": 1745756311,
  "iss": "http://127.0.0.1:8999",
  "aud": "http://127.0.0.1:8999"
}
*/
interface JwtPayload {
  exp: number;
}

// 检查 token 是否过期
export function IsTokenExpired(token: string): boolean {
  try {
    const decoded = jwtDecode<JwtPayload>(token);

    const currentTime = Math.floor(Date.now() / 1000);

    // 倒退一分钟，避免刷新来不及
    return decoded.exp < currentTime - 60;
  } catch (error) {
    // 如果无法解码 token，认为 token 已过期或无效
    console.error("Invalid Token:", error);
    return true;
  }
}
