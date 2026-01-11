import { MessageInstance } from "antd/es/message/interface";
import { BusinessValidationResult } from "../apiClient/models";
import { FormInstance } from "antd";

// 代理请求处理错误
export const proxyRequestError = (
  error: any,
  messageApi: MessageInstance,
  defaultMessage?: string
) => {
  console.error("Login error:", error);
  const businessError = error as BusinessValidationResult;
  if (businessError === undefined || businessError == null) {
    if (defaultMessage !== undefined) {
      messageApi.error(defaultMessage);
      return;
    }

    messageApi.error(error!.toString());

    return;
  }

  // 模型验证错误
  if (!businessError.detail) {
    // 处理字段级别的错误，只显示第一条错误
    if (businessError.errors && businessError.errors.length > 0) {
      const firstError = businessError.errors.find(
        (err) => err.name && err.errors && err.errors.length > 0
      );

      if (firstError) {
        messageApi.error(firstError.errors![0]);
      }
    }
    return;
  }

  messageApi.error(businessError.detail);
};

// 代理请求处理表单错误
export const proxyFormRequestError = (
  error: any,
  messageApi: MessageInstance,
  form: FormInstance,
  defaultMessage?: string
) => {
  console.error("Form error:", error);
  const businessError = error as BusinessValidationResult;
  if (businessError === undefined || businessError == null) {
    if (defaultMessage) {
      messageApi.error(defaultMessage);
      return;
    }

    messageApi.error(error!.toString());
    return;
  }

  // 显示主要错误信息
  if (businessError.detail) {
    messageApi.error(businessError.detail);
  }

  // 处理字段级别的错误
  if (businessError.errors && businessError.errors.length > 0) {
    const adaptedErrors = businessError.errors
      .filter((err) => err.name && err.errors && err.errors.length > 0)
      .map((err) => ({
        name: err.name!,
        errors: err.errors!,
      }));

    if (adaptedErrors.length > 0) {
      form.setFields(adaptedErrors);
    }
  }
};
