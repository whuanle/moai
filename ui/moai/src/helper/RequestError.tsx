import { MessageInstance } from "antd/es/message/interface";
import { BusinessValidationResult } from "../apiClient/models";
import { FormInstance } from "antd";

// 代理请求处理错误
export const proxyRequestError = (error: any, messageApi: MessageInstance) => {
    console.error("Login error:", error);
    const businessError = error as BusinessValidationResult;
    if (businessError === undefined || businessError == null) {
      messageApi.error(error!.toString());
  
      return;
    }
  
    messageApi.error(businessError.detail);
  };
  
  // 代理请求处理表单错误
  export const proxyFormRequestError = (
    error: any,
    messageApi: MessageInstance,
    form: FormInstance
  ) => {
    console.error("Login error:", error);
    const businessError = error as BusinessValidationResult;
    if (businessError === undefined || businessError == null) {
      messageApi.error(error!.toString());
  
      return;
    }
  
    messageApi.error(businessError.detail);
  
    if (businessError.errors && businessError.errors.length > 0) {
      const adaptedErrors = businessError.errors.map((err) => ({
        name: err.name ?? [],
        errors: err.errors ?? [],
      }));
      form.setFields(adaptedErrors);
    }
  };
  