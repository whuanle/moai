/**
 * 运行测试模态框组件
 * 打开时自动获取插件运行示例参数
 */
import { useState, useEffect, useCallback } from "react";
import {
  Modal,
  Form,
  Input,
  Button,
  Space,
  Typography,
  Tag,
  Spin,
  Alert,
  Divider,
  message,
} from "antd";
import {
  PlayCircleOutlined,
  ExpandOutlined,
  CodeOutlined,
  CheckCircleOutlined,
  CloseCircleOutlined,
  ApiOutlined,
} from "@ant-design/icons";
import type { NativePluginInfo } from "../../../../../apiClient/models";
import { GetApiClient } from "../../../../ServiceClient";
import { proxyRequestError } from "../../../../../helper/RequestError";
import "./RunTestModal.css";

/** 运行目标：pluginId 或 templatePluginKey */
export interface RunTarget {
  pluginId?: number;
  pluginName?: string;
  templatePluginKey?: string;
}

interface RunTestModalProps {
  open: boolean;
  target: RunTarget | null;
  onClose: () => void;
}

/** JSON 格式化辅助函数 */
function formatJsonString(value: string): string {
  if (!value) return "";
  try {
    const parsed = JSON.parse(value);
    // 如果还是字符串，则说明本身真的是字符串，则返回解析后端字符串
    if (typeof parsed === "string") {
      return parsed;
    }

    // 如果是对象，则保留原本的 json 格式
    if (typeof parsed === "object") {
      return value;
    }
    
  } catch {
    return value;
  }

  return value;
}

export default function RunTestModal({
  open,
  target,
  onClose,
}: RunTestModalProps) {
  const [messageApi, contextHolder] = message.useMessage();

  // 加载状态
  const [loading, setLoading] = useState(false);
  const [runLoading, setRunLoading] = useState(false);

  // 数据状态
  const [pluginInfo, setPluginInfo] = useState<NativePluginInfo | null>(null);
  const [paramsValue, setParamsValue] = useState("");
  const [result, setResult] = useState<{ success: boolean; message: string } | null>(null);

  // 全屏查看
  const [fullscreenVisible, setFullscreenVisible] = useState(false);

  // 获取插件运行示例参数
  const fetchPluginParams = useCallback(async () => {
    if (!target) return;

    setLoading(true);
    setParamsValue("");
    setResult(null);
    setPluginInfo(null);

    try {
      const client = GetApiClient();

      // 获取模板参数（包含示例值和描述）
      if (target.templatePluginKey) {
        const paramsResponse = await client.api.admin.native_plugin.template_params.post({
          templatePluginKey: target.templatePluginKey,
        });

        if (paramsResponse) {
          setPluginInfo({
            pluginId: target.pluginId,
            pluginName: target.pluginName || "",
            templatePluginKey: target.templatePluginKey,
            description: paramsResponse.description || undefined,
          });
          setParamsValue(formatJsonString(paramsResponse.exampleValue || ""));
        }
      } else if (target.pluginId) {
        // 如果只有 pluginId，先获取插件详情
        const detailResponse = await client.api.admin.native_plugin.detail.post({
          pluginId: target.pluginId,
        });

        if (detailResponse?.templatePluginKey) {
          const paramsResponse = await client.api.admin.native_plugin.template_params.post({
            templatePluginKey: detailResponse.templatePluginKey,
          });

          if (paramsResponse) {
            setPluginInfo({
              pluginId: target.pluginId,
              pluginName: detailResponse.pluginName || target.pluginName || "",
              templatePluginKey: detailResponse.templatePluginKey,
              description: paramsResponse.description || detailResponse.description || undefined,
            });
            setParamsValue(formatJsonString(paramsResponse.exampleValue || ""));
          }
        }
      }
    } catch (error) {
      console.error("获取运行参数失败:", error);
      proxyRequestError(error, messageApi, "获取运行参数失败");
    } finally {
      setLoading(false);
    }
  }, [target, messageApi]);

  // 打开时加载数据
  useEffect(() => {
    if (open && target) {
      fetchPluginParams();
    }
  }, [open, target]);

  // 运行插件
  const handleRun = useCallback(async () => {
    if (!pluginInfo) {
      messageApi.error("请先选择插件");
      return;
    }
    if (!paramsValue.trim()) {
      messageApi.error("请输入运行参数");
      return;
    }

    try {
      setRunLoading(true);
      setResult(null);

      const client = GetApiClient();
      const response = await client.api.admin.native_plugin.run_test.post({
        templatePluginKey: pluginInfo.templatePluginKey || undefined,
        pluginId: pluginInfo.pluginId || undefined,
        params: paramsValue,
      });

      if (response) {
        const resultMessage = formatJsonString(response.response || "");
        setResult({
          success: response.isSuccess!,
          message: resultMessage,
        });
        messageApi.success(response.isSuccess ? "插件运行成功" : "插件运行失败");
      }
    } catch (error: any) {
      console.error("运行插件失败:", error);
      const errorMsg =
        error?.detail ||
        error?.message ||
        (error instanceof Error ? error.message : "运行插件时发生错误");
      setResult({ success: false, message: errorMsg });
      proxyRequestError(error, messageApi, "运行插件失败");
    } finally {
      setRunLoading(false);
    }
  }, [pluginInfo, paramsValue, messageApi]);

  // 关闭时重置状态
  const handleClose = useCallback(() => {
    setFullscreenVisible(false);
    setPluginInfo(null);
    setParamsValue("");
    setResult(null);
    onClose();
  }, [onClose]);

  return (
    <>
      {contextHolder}
      <Modal
        title={
          <div className="run-modal-title">
            <ApiOutlined className="run-modal-title-icon" />
            <span>运行测试</span>
            {pluginInfo && (
              <Tag color="purple" className="run-modal-title-tag">
                {pluginInfo.pluginName}
              </Tag>
            )}
          </div>
        }
        open={open}
        onCancel={handleClose}
        maskClosable={false}
        width={900}
        className="run-test-modal"
        footer={
          <div className="run-modal-footer">
            <Button onClick={handleClose}>关闭</Button>
            <Button
              type="primary"
              icon={<PlayCircleOutlined />}
              onClick={handleRun}
              loading={runLoading}
              disabled={loading}
            >
              运行测试
            </Button>
          </div>
        }
      >
        <Spin spinning={loading}>
          <div className="run-modal-content">
            {/* 插件描述 */}
            {pluginInfo?.description && (
              <Alert
                message="插件说明"
                description={
                  <Typography.Text className="run-modal-description">
                    {pluginInfo.description}
                  </Typography.Text>
                }
                type="info"
                showIcon
                icon={<CodeOutlined />}
                className="run-modal-alert"
              />
            )}

            {/* 输入参数 */}
            <div className="run-modal-section">
              <div className="run-modal-section-header">
                <Typography.Text strong>输入参数</Typography.Text>
                <Typography.Text type="secondary" className="run-modal-section-hint">
                  字符串直接输入，对象使用 JSON 格式
                </Typography.Text>
              </div>
              <Input.TextArea
                rows={10}
                value={paramsValue}
                onChange={(e) => setParamsValue(e.target.value)}
                placeholder="请输入运行参数..."
                className="run-params-textarea"
              />
            </div>

            {/* 运行结果 */}
            {result && (
              <>
                <Divider className="run-modal-divider" />
                <div className="run-modal-section">
                  <div className="run-modal-section-header">
                    <Space>
                      {result.success ? (
                        <CheckCircleOutlined className="run-result-icon-success" />
                      ) : (
                        <CloseCircleOutlined className="run-result-icon-error" />
                      )}
                      <Typography.Text strong>
                        {result.success ? "运行成功" : "运行失败"}
                      </Typography.Text>
                    </Space>
                    <Button
                      type="link"
                      size="small"
                      icon={<ExpandOutlined />}
                      onClick={() => setFullscreenVisible(true)}
                    >
                      全屏查看
                    </Button>
                  </div>
                  <Input.TextArea
                    readOnly
                    rows={12}
                    value={result.message}
                    className={`run-result-textarea ${result.success ? "run-result-success" : "run-result-error"
                      }`}
                  />
                </div>
              </>
            )}
          </div>
        </Spin>
      </Modal>

      {/* 全屏查看模态框 */}
      <Modal
        title={
          <div className="run-modal-title">
            <span>运行结果</span>
            {pluginInfo && (
              <Tag color="purple" className="run-modal-title-tag">
                {pluginInfo.pluginName}
              </Tag>
            )}
            {result && (
              <Tag
                color={result.success ? "success" : "error"}
                icon={result.success ? <CheckCircleOutlined /> : <CloseCircleOutlined />}
              >
                {result.success ? "成功" : "失败"}
              </Tag>
            )}
          </div>
        }
        open={fullscreenVisible}
        onCancel={() => setFullscreenVisible(false)}
        maskClosable={false}
        width="90%"
        style={{ top: 20 }}
        className="run-test-modal-fullscreen"
        footer={
          <Button onClick={() => setFullscreenVisible(false)}>关闭</Button>
        }
      >
        {result && (
          <Input.TextArea
            readOnly
            rows={30}
            value={result.message}
            className={`run-result-textarea ${result.success ? "run-result-success" : "run-result-error"
              }`}
          />
        )}
      </Modal>
    </>
  );
}
