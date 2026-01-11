import { useState, useEffect } from "react";
import {
  Card,
  Form,
  Input,
  Button,
  message,
  Upload,
  Avatar,
  Typography,
} from "antd";
import type { UploadProps } from "antd";
import {
  SaveOutlined,
  TeamOutlined,
  CameraOutlined,
  LoadingOutlined,
} from "@ant-design/icons";
import { GetApiClient } from "../ServiceClient";
import { useParams, useOutletContext } from "react-router";
import { proxyFormRequestError, proxyRequestError } from "../../helper/RequestError";
import { GetFileMd5 } from "../../helper/Md5Helper";
import type { QueryTeamListQueryResponseItem, TeamRole } from "../../apiClient/models";
import { TeamRoleObject } from "../../apiClient/models";
import "./TeamSettings.css";

const { Text } = Typography;

// 原始团队信息类型
interface OriginalTeamInfo {
  name: string;
  description?: string;
  avatarKey?: string;
}

interface OutletContextType {
  teamInfo: QueryTeamListQueryResponseItem | null;
  myRole: TeamRole;
  refreshTeamInfo: () => void;
}

export default function TeamSettings() {
  const { id } = useParams();
  const { teamInfo, myRole, refreshTeamInfo } = useOutletContext<OutletContextType>();
  const [loading, setLoading] = useState(false);
  const [avatarUploading, setAvatarUploading] = useState(false);
  const [avatarUrl, setAvatarUrl] = useState<string | undefined>();
  const [originalTeamInfo, setOriginalTeamInfo] = useState<OriginalTeamInfo | null>(null);
  const [messageApi, contextHolder] = message.useMessage();
  const [form] = Form.useForm();

  const apiClient = GetApiClient();
  const canManage = myRole === TeamRoleObject.Owner || myRole === TeamRoleObject.Admin;

  useEffect(() => {
    if (teamInfo) {
      setAvatarUrl(teamInfo.avatar || undefined);
      setOriginalTeamInfo({
        name: teamInfo.name || "",
        description: teamInfo.description || undefined,
        avatarKey: teamInfo.avatarKey || undefined,
      });
      form.setFieldsValue({
        name: teamInfo.name,
        description: teamInfo.description,
        avatarKey: teamInfo.avatarKey,
      });
    }
  }, [teamInfo, form]);

  const handleAvatarUpload = async (file: File) => {
    if (!id || !originalTeamInfo) return;
    setAvatarUploading(true);
    try {
      const client = GetApiClient();
      
      // 1. 计算文件 MD5
      const md5 = await GetFileMd5(file);
      
      // 2. 获取预上传地址
      const preUploadResponse = await client.api.storage.public.pre_upload_image.post({
        fileName: file.name,
        fileSize: file.size,
        contentType: file.type,
        mD5: md5,
      });

      if (!preUploadResponse) {
        throw new Error('获取上传地址失败');
      }

      const avatarPath = preUploadResponse.objectKey!;

      // 3. 如果文件不存在，需要上传
      if (!preUploadResponse.isExist) {
        // 上传文件到预签名地址
        const uploadResponse = await fetch(preUploadResponse.uploadUrl!, {
          method: 'PUT',
          body: file,
          headers: {
            'Content-Type': file.type,
          },
        });

        if (!uploadResponse.ok) {
          throw new Error('上传文件失败');
        }

        // 完成上传回调
        await client.api.storage.complate_url.post({
          fileId: preUploadResponse.fileId,
          isSuccess: true,
        });
      }

      // 4. 使用原始团队信息调用更新接口
      await client.api.team.update.post({
        teamId: parseInt(id),
        name: originalTeamInfo.name,
        description: originalTeamInfo.description,
        avatar: avatarPath,
      });

      // 更新表单中的 avatarKey
      form.setFieldValue("avatarKey", avatarPath);
      messageApi.success('头像更新成功');
      refreshTeamInfo();
    } catch (error) {
      console.error('上传头像失败:', error);
      proxyRequestError(error, messageApi, '上传头像失败');
    } finally {
      setAvatarUploading(false);
    }
  };

  const beforeUpload: UploadProps['beforeUpload'] = (file) => {
    const isImage = file.type.startsWith('image/');
    if (!isImage) {
      messageApi.error('只能上传图片文件');
      return false;
    }
    
    const isLt5M = file.size / 1024 / 1024 < 5;
    if (!isLt5M) {
      messageApi.error('图片大小不能超过 5MB');
      return false;
    }
    
    handleAvatarUpload(file);
    return false;
  };

  const handleSubmit = async (values: { name: string; description?: string; avatarKey?: string }) => {
    if (!id) return;

    try {
      setLoading(true);
      await apiClient.api.team.update.post({
        teamId: parseInt(id),
        name: values.name,
        description: values.description,
        avatar: values.avatarKey,
      });

      messageApi.success("保存成功");
      refreshTeamInfo();
    } catch (error) {
      proxyFormRequestError(error, messageApi, form, "保存失败");
    } finally {
      setLoading(false);
    }
  };

  if (!canManage) {
    return (
      <Card>
        <Text type="secondary">您没有权限修改团队设置</Text>
      </Card>
    );
  }

  return (
    <>
      {contextHolder}
      <Card title="团队设置">
        <Form
          form={form}
          layout="vertical"
          onFinish={handleSubmit}
          className="team-settings-form"
        >
          <Form.Item name="avatarKey" hidden>
            <Input />
          </Form.Item>
          <Form.Item label="团队头像">
            <Upload
              showUploadList={false}
              beforeUpload={beforeUpload}
              accept="image/*"
              disabled={avatarUploading}
            >
              <div className="team-avatar-upload-wrapper">
                <Avatar
                  size={80}
                  src={avatarUrl}
                  icon={avatarUploading ? <LoadingOutlined /> : <TeamOutlined />}
                />
                <div className="team-avatar-upload-overlay">
                  {avatarUploading ? <LoadingOutlined /> : <CameraOutlined />}
                  <span>{avatarUploading ? '上传中...' : '更换头像'}</span>
                </div>
              </div>
            </Upload>
          </Form.Item>

          <Form.Item
            name="name"
            label="团队名称"
            rules={[{ required: true, message: "请输入团队名称" }]}
          >
            <Input placeholder="请输入团队名称" maxLength={100} />
          </Form.Item>

          <Form.Item name="description" label="团队描述">
            <Input.TextArea
              placeholder="请输入团队描述"
              maxLength={500}
              rows={4}
              showCount
            />
          </Form.Item>

          <Form.Item>
            <Button
              type="primary"
              htmlType="submit"
              icon={<SaveOutlined />}
              loading={loading}
            >
              保存设置
            </Button>
          </Form.Item>
        </Form>
      </Card>
    </>
  );
}
