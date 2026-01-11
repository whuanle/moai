import { useState, useEffect } from "react";
import {
  Form,
  Input,
  Button,
  message,
  Switch,
  InputNumber,
  Select,
  Tooltip,
  App,
  Upload,
  Avatar,
  Alert,
  Card,
} from "antd";
import type { UploadProps } from "antd";
import type { MessageInstance } from "antd/es/message/interface";
import {
  BookOutlined,
  CameraOutlined,
  LoadingOutlined,
  SaveOutlined,
  DeleteOutlined,
  LockOutlined,
  GlobalOutlined,
  EyeInvisibleOutlined,
  SettingOutlined,
} from "@ant-design/icons";
import { GetApiClient } from "../ServiceClient";
import { useParams } from "react-router";
import { proxyRequestError, proxyFormRequestError } from "../../helper/RequestError";
import { useAiModelList } from "./wiki_hooks";
import { GetFileMd5 } from "../../helper/Md5Helper";
import "./WikiSettings.css";

// 表单值类型
interface WikiFormValues {
  name: string;
  description: string;
  isPublic: boolean;
  embeddingDimensions?: number;
  embeddingModelId?: number;
  avatarKey?: string;
}

// 原始知识库信息类型
interface OriginalWikiInfo {
  name: string;
  description: string;
  isPublic: boolean;
  avatarKey?: string;
}

// 头像上传组件
interface AvatarUploaderProps {
  avatarUrl?: string;
  uploading: boolean;
  onUpload: (file: File) => void;
  messageApi: MessageInstance;
}

function AvatarUploader({ avatarUrl, uploading, onUpload, messageApi }: AvatarUploaderProps) {
  const beforeUpload: UploadProps["beforeUpload"] = (file) => {
    const isImage = file.type.startsWith("image/");
    if (!isImage) {
      messageApi.error("只能上传图片文件");
      return false;
    }

    const isLt5M = file.size / 1024 / 1024 < 5;
    if (!isLt5M) {
      messageApi.error("图片大小不能超过 5MB");
      return false;
    }

    onUpload(file);
    return false;
  };

  return (
    <Upload
      showUploadList={false}
      beforeUpload={beforeUpload}
      accept="image/*"
      disabled={uploading}
    >
      <div className="wiki-settings-avatar-wrapper">
        <Avatar
          size={64}
          src={avatarUrl}
          icon={uploading ? <LoadingOutlined spin /> : <BookOutlined />}
          className="wiki-settings-avatar"
        />
        <div className="wiki-settings-avatar-overlay">
          {uploading ? <LoadingOutlined spin /> : <CameraOutlined />}
        </div>
      </div>
    </Upload>
  );
}

export default function WikiSettings() {
  const [form] = Form.useForm<WikiFormValues>();
  const [messageApi, contextHolder] = message.useMessage();
  const { modal } = App.useApp();
  const { id } = useParams();
  const apiClient = GetApiClient();

  // 状态
  const [loading, setLoading] = useState(false);
  const [clearingVectors, setClearingVectors] = useState(false);
  const [isLock, setIsLock] = useState(false);
  const [avatarUrl, setAvatarUrl] = useState<string>();
  const [avatarUploading, setAvatarUploading] = useState(false);
  const [originalWikiInfo, setOriginalWikiInfo] = useState<OriginalWikiInfo | null>(null);

  // 使用共享 hook 获取向量化模型列表
  const { modelList: embeddingModels, loading: modelsLoading, fetchModelList: fetchEmbeddingModels } = useAiModelList(parseInt(id || "0"), "embedding");

  // 获取知识库信息
  const fetchWikiInfo = async () => {
    if (!id) {
      messageApi.error("缺少必要的参数");
      return;
    }

    try {
      setLoading(true);
      const response = await apiClient.api.wiki.query_wiki_info.post({
        wikiId: parseInt(id),
      });

      if (response) {
        setIsLock(response.isLock ?? false);
        setAvatarUrl(response.avatar ?? undefined);
        setOriginalWikiInfo({
          name: response.name ?? "",
          description: response.description ?? "",
          isPublic: response.isPublic ?? false,
          avatarKey: response.avatarKey ?? undefined,
        });
        form.setFieldsValue({
          name: response.name ?? undefined,
          description: response.description ?? undefined,
          isPublic: response.isPublic ?? undefined,
          embeddingDimensions: response.embeddingDimensions ?? undefined,
          embeddingModelId: response.embeddingModelId === 0 ? undefined : (response.embeddingModelId ?? undefined),
          avatarKey: response.avatarKey ?? undefined,
        });
      }
    } catch (error) {
      console.error("获取知识库信息失败:", error);
      proxyRequestError(error, messageApi, "获取知识库信息失败");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchWikiInfo();
    fetchEmbeddingModels();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [id]);

  // 处理头像上传
  const handleAvatarUpload = async (file: File) => {
    if (!id || !originalWikiInfo) return;
    
    setAvatarUploading(true);
    try {
      const md5 = await GetFileMd5(file);
      const preUploadResponse = await apiClient.api.storage.public.pre_upload_image.post({
        fileName: file.name,
        fileSize: file.size,
        contentType: file.type,
        mD5: md5,
      });

      if (!preUploadResponse) {
        throw new Error("获取上传地址失败");
      }

      const avatarPath = preUploadResponse.objectKey!;

      if (!preUploadResponse.isExist) {
        const uploadResponse = await fetch(preUploadResponse.uploadUrl!, {
          method: "PUT",
          body: file,
          headers: { "Content-Type": file.type },
        });

        if (!uploadResponse.ok) {
          throw new Error("上传文件失败");
        }

        await apiClient.api.storage.complate_url.post({
          fileId: preUploadResponse.fileId,
          isSuccess: true,
        });
      }

      await apiClient.api.wiki.manager.update_wiki_config.post({
        wikiId: parseInt(id),
        name: originalWikiInfo.name,
        description: originalWikiInfo.description,
        isPublic: originalWikiInfo.isPublic,
        avatar: avatarPath,
      });

      form.setFieldValue("avatarKey", avatarPath);
      messageApi.success("头像更新成功");
      await fetchWikiInfo();
    } catch (error) {
      console.error("上传头像失败:", error);
      proxyRequestError(error, messageApi, "上传头像失败");
    } finally {
      setAvatarUploading(false);
    }
  };

  // 提交表单
  const handleSubmit = async (values: WikiFormValues) => {
    if (!id) {
      messageApi.error("缺少必要的参数");
      return;
    }

    try {
      setLoading(true);
      const updateData: Record<string, unknown> = {
        wikiId: parseInt(id),
        name: values.name,
        description: values.description,
        isPublic: values.isPublic,
        avatar: values.avatarKey,
      };

      if (!isLock) {
        updateData.embeddingDimensions = values.embeddingDimensions;
        updateData.embeddingModelId = values.embeddingModelId;
      }

      await apiClient.api.wiki.manager.update_wiki_config.post(updateData);
      messageApi.success("保存成功");
      await fetchWikiInfo();
    } catch (error) {
      console.error("保存失败:", error);
      proxyFormRequestError(error, messageApi, form, "保存失败");
    } finally {
      setLoading(false);
    }
  };

  // 清空向量
  const clearWikiVectors = async () => {
    if (!id) {
      messageApi.error("缺少必要的参数");
      return;
    }

    try {
      setClearingVectors(true);
      await apiClient.api.wiki.document.clear_embeddingt.post({
        wikiId: parseInt(id),
        clearAllDocuments: true,
        isAutoDeleteIndex: true,
      });
      messageApi.success("知识库向量已清空");
      await fetchWikiInfo();
    } catch (error) {
      console.error("清空向量失败:", error);
      proxyRequestError(error, messageApi, "清空向量失败");
    } finally {
      setClearingVectors(false);
    }
  };

  const handleClearVectors = () => {
    modal.confirm({
      title: "确认清空知识库向量",
      content: "此操作将删除所有已生成的向量数据，操作不可逆。确定要继续吗？",
      okText: "确定清空",
      cancelText: "取消",
      okType: "danger",
      maskClosable: false,
      onOk: clearWikiVectors,
    });
  };

  return (
    <div className="wiki-settings-wrapper">
      {contextHolder}
      <Form
        form={form}
        layout="vertical"
        onFinish={handleSubmit}
        className="wiki-settings-form"
      >
        {/* 基本信息卡片 */}
        <Card
          title={
            <div className="wiki-settings-card-header">
              <span className="wiki-settings-card-title">
                <BookOutlined className="wiki-settings-card-icon" />
                知识库设置
              </span>
              <span className="wiki-settings-card-subtitle">配置知识库的基本信息</span>
            </div>
          }
          className="wiki-settings-card"
        >
          <div className="wiki-settings-avatar-row">
            <Form.Item name="avatarKey" hidden>
              <Input />
            </Form.Item>
            <AvatarUploader
              avatarUrl={avatarUrl}
              uploading={avatarUploading}
              onUpload={handleAvatarUpload}
              messageApi={messageApi}
            />
            <div className="wiki-settings-avatar-info">
              <div className="wiki-settings-avatar-label">知识库头像</div>
              <div className="wiki-settings-avatar-tip">支持 JPG、PNG 格式，大小不超过 5MB</div>
            </div>
          </div>

          <Form.Item
            name="name"
            label="知识库名称"
            rules={[{ required: true, message: "请输入知识库名称" }]}
          >
            <Input placeholder="请输入知识库名称" maxLength={50} showCount />
          </Form.Item>

          <Form.Item
            name="description"
            label="知识库描述"
            rules={[{ required: true, message: "请输入知识库描述" }]}
          >
            <Input.TextArea
              placeholder="请输入知识库描述，帮助用户了解知识库的用途"
              rows={3}
              maxLength={500}
              showCount
            />
          </Form.Item>

          <Form.Item name="isPublic" label="可见性" valuePropName="checked">
            <Switch
              checkedChildren={<><GlobalOutlined /> 公开</>}
              unCheckedChildren={<><EyeInvisibleOutlined /> 私有</>}
            />
          </Form.Item>
        </Card>

        {/* 向量化配置卡片 */}
        <Card
          title={
            <span className="wiki-settings-card-title">
              {isLock ? (
                <LockOutlined className="wiki-settings-card-icon locked" />
              ) : (
                <SettingOutlined className="wiki-settings-card-icon" />
              )}
              向量化配置
              {isLock && <span className="wiki-settings-lock-badge">已锁定</span>}
            </span>
          }
          className="wiki-settings-card"
        >
          {isLock && (
            <Alert
              type="warning"
              showIcon
              message="配置已锁定"
              description="知识库已生成向量数据，向量化配置已锁定。如需修改，请先清空知识库向量。"
              className="wiki-settings-lock-alert"
            />
          )}

          <Form.Item
            name="embeddingModelId"
            label="向量化模型"
            rules={[{ required: !isLock, message: "请选择向量化模型" }]}
            tooltip="选择用于文档向量化的 AI 模型"
          >
            <Select
              placeholder="请选择向量化模型"
              disabled={isLock}
              loading={modelsLoading}
              showSearch
              optionFilterProp="label"
              options={embeddingModels.map((model) => ({
                label: model.name || `模型 ${model.id}`,
                value: model.id,
                disabled: !model.id,
              }))}
            />
          </Form.Item>

          <Form.Item
            name="embeddingDimensions"
            label="向量维度"
            rules={[{ required: !isLock, message: "请输入向量维度" }]}
            tooltip="向量的维度大小，需与所选模型匹配"
          >
            <InputNumber
              min={1}
              max={4096}
              disabled={isLock}
              style={{ width: "100%" }}
              placeholder="请输入向量维度"
            />
          </Form.Item>
        </Card>

        {/* 危险操作卡片 */}
        <Card
          title={
            <span className="wiki-settings-card-title danger">
              <DeleteOutlined className="wiki-settings-card-icon" />
              危险操作
            </span>
          }
          className="wiki-settings-card wiki-settings-danger-card"
        >
          <div className="wiki-settings-danger-item">
            <div className="wiki-settings-danger-info">
              <div className="wiki-settings-danger-title">清空知识库向量</div>
              <div className="wiki-settings-danger-desc">
                删除所有已生成的向量数据，此操作不可逆。清空后可重新配置向量化参数。
              </div>
            </div>
            <Tooltip title="清空后将解锁向量化配置">
              <Button
                danger
                loading={clearingVectors}
                onClick={handleClearVectors}
                icon={<DeleteOutlined />}
              >
                清空向量
              </Button>
            </Tooltip>
          </div>
        </Card>

        {/* 提交按钮 */}
        <div className="wiki-settings-footer">
          <Button
            type="primary"
            htmlType="submit"
            loading={loading}
            icon={<SaveOutlined />}
            size="large"
          >
            保存设置
          </Button>
        </div>
      </Form>
    </div>
  );
}
