import { useState, useCallback } from 'react';
import { useParams } from 'react-router';
import {
  Card,
  Button,
  message,
  Typography,
  Form,
  Input,
  Upload,
  Radio,
  Space,
  Spin,
  Alert,
} from 'antd';
import { UploadOutlined, LinkOutlined, CheckCircleOutlined } from '@ant-design/icons';
import type { UploadFile, RcFile } from 'antd/es/upload/interface';
import { GetApiClient } from '../../../ServiceClient';
import { proxyFormRequestError } from '../../../../helper/RequestError';
import { GetFileMd5 } from '../../../../helper/Md5Helper';
import { FileTypeHelper } from '../../../../helper/FileTypeHelper';
import type { PreUploadTempFileCommand } from '../../../../apiClient/models';
import '../../../../styles/theme.css';

const { Text, Title } = Typography;

type ImportType = 'url' | 'file';

interface ImportResult {
  success: boolean;
  message: string;
  source?: string;
}

export default function OpenApiListPage() {
  const { id } = useParams<{ id: string }>();
  const wikiId = id ? parseInt(id) : undefined;

  const [loading, setLoading] = useState(false);
  const [messageApi, contextHolder] = message.useMessage();
  const [form] = Form.useForm();
  const [importType, setImportType] = useState<ImportType>('url');
  const [fileList, setFileList] = useState<UploadFile[]>([]);
  const [importResult, setImportResult] = useState<ImportResult | null>(null);

  // 上传文件到服务器（使用临时文件上传接口）
  const uploadFile = async (file: RcFile): Promise<number | null> => {
    const client = GetApiClient();
    const md5 = await GetFileMd5(file);

    // 预上传获取签名URL
    const preUploadBody: PreUploadTempFileCommand = {
      contentType: FileTypeHelper.getFileType(file),
      fileName: file.name,
      mD5: md5,
      fileSize: file.size,
    };

    const preUploadResponse = await client.api.storage.public.pre_upload_temp.post(preUploadBody);

    if (!preUploadResponse) {
      throw new Error('获取预签名URL失败');
    }

    // 如果文件已存在，直接返回 fileId
    if (preUploadResponse.isExist === true) {
      return preUploadResponse.fileId || null;
    }

    if (!preUploadResponse.uploadUrl) {
      throw new Error('获取预签名URL失败');
    }

    // 上传文件到预签名URL
    const uploadResponse = await fetch(preUploadResponse.uploadUrl, {
      method: 'PUT',
      body: file,
      headers: {
        'Content-Type': FileTypeHelper.getFileType(file),
      },
    });

    if (uploadResponse.status !== 200) {
      throw new Error(uploadResponse.statusText);
    }

    // 完成上传回调
    await client.api.storage.complate_url.post({
      fileId: preUploadResponse.fileId,
      isSuccess: true,
    });

    return preUploadResponse.fileId || null;
  };

  // 处理导入
  const handleImport = useCallback(async () => {
    if (!wikiId) {
      messageApi.error('缺少知识库ID');
      return;
    }

    // 清除之前的结果
    setImportResult(null);

    try {
      await form.validateFields();
      const values = form.getFieldsValue();

      setLoading(true);
      const client = GetApiClient();

      let source = '';

      if (importType === 'url') {
        // URL 导入
        if (!values.url?.trim()) {
          messageApi.error('请输入 OpenAPI 文档 URL');
          return;
        }

        source = values.url.trim();
        await client.api.wiki.plugin.openapi.import_api.post({
          wikiId: wikiId,
          openApiSpecUrl: source,
        });
      } else {
        // 文件导入
        if (fileList.length === 0) {
          messageApi.error('请选择要上传的文件');
          return;
        }

        const file = fileList[0].originFileObj as RcFile;
        source = file.name;
        const fileId = await uploadFile(file);

        if (!fileId) {
          throw new Error('文件上传失败');
        }

        await client.api.wiki.plugin.openapi.import_api.post({
          wikiId: wikiId,
          fileId: fileId,
        });
      }

      messageApi.success('OpenAPI 文档导入成功');

      // 设置成功结果
      setImportResult({
        success: true,
        message: 'OpenAPI 文档导入成功，系统正在解析 API 接口信息并生成知识库文档。',
        source: source,
      });

      // 重置表单
      form.resetFields();
      setFileList([]);
    } catch (error) {
      console.error('导入失败:', error);
      proxyFormRequestError(error, messageApi, form, '导入失败');

      // 设置失败结果
      setImportResult({
        success: false,
        message: error instanceof Error ? error.message : '导入失败，请检查文档格式是否正确',
      });
    } finally {
      setLoading(false);
    }
  }, [wikiId, importType, fileList, form, messageApi]);

  // 文件选择前的校验
  const beforeUpload = (file: RcFile) => {
    const isValidType =
      file.name.endsWith('.json') ||
      file.name.endsWith('.yaml') ||
      file.name.endsWith('.yml');

    if (!isValidType) {
      messageApi.error('仅支持 JSON、YAML 格式的 OpenAPI 文档');
      return Upload.LIST_IGNORE;
    }

    const isLt10M = file.size / 1024 / 1024 < 10;
    if (!isLt10M) {
      messageApi.error('文件大小不能超过 10MB');
      return Upload.LIST_IGNORE;
    }

    // 清除之前的结果
    setImportResult(null);

    return false; // 阻止自动上传
  };

  // 文件列表变化
  const handleFileChange = ({ fileList: newFileList }: { fileList: UploadFile[] }) => {
    setFileList(newFileList.slice(-1)); // 只保留最后一个文件
  };

  if (!wikiId) {
    return (
      <div className="moai-empty">
        <Text type="secondary">缺少知识库ID</Text>
      </div>
    );
  }

  return (
    <div className="page-container">
      {contextHolder}

      <div className="moai-page-header">
        <Title level={4} className="moai-page-title">OpenAPI 文档导入</Title>
        <Text type="secondary" className="moai-page-subtitle">
          导入 OpenAPI (Swagger) 规范文档，系统将自动解析 API 接口信息并生成知识库文档。支持 JSON 或 YAML 格式。
        </Text>
      </div>

      <Card className="moai-card">
        <Spin spinning={loading}>
          <Form form={form} layout="vertical" style={{ maxWidth: 600 }}>
            <Form.Item label="导入方式">
              <Radio.Group
                value={importType}
                onChange={(e) => {
                  setImportType(e.target.value);
                  setImportResult(null);
                }}
              >
                <Radio.Button value="url">
                  <LinkOutlined /> URL 导入
                </Radio.Button>
                <Radio.Button value="file">
                  <UploadOutlined /> 文件上传
                </Radio.Button>
              </Radio.Group>
            </Form.Item>

            {importType === 'url' ? (
              <Form.Item
                name="url"
                label="OpenAPI 文档 URL"
                rules={[
                  { required: true, message: '请输入 OpenAPI 文档 URL' },
                  { type: 'url', message: '请输入有效的 URL 地址' },
                ]}
              >
                <Input
                  placeholder="https://example.com/openapi.json"
                  allowClear
                  onChange={() => setImportResult(null)}
                />
              </Form.Item>
            ) : (
              <Form.Item
                label="上传文件"
                required
                extra="支持 JSON、YAML 格式，文件大小不超过 10MB"
              >
                <Upload
                  fileList={fileList}
                  beforeUpload={beforeUpload}
                  onChange={handleFileChange}
                  maxCount={1}
                  accept=".json,.yaml,.yml"
                >
                  <Button icon={<UploadOutlined />}>选择文件</Button>
                </Upload>
              </Form.Item>
            )}

            {/* 导入结果显示 */}
            {importResult && (
              <Form.Item>
                <Alert
                  type={importResult.success ? 'success' : 'error'}
                  showIcon
                  icon={importResult.success ? <CheckCircleOutlined /> : undefined}
                  message={importResult.success ? '导入成功' : '导入失败'}
                  description={
                    <div>
                      <div>{importResult.message}</div>
                      {importResult.source && (
                        <div style={{ marginTop: 8 }}>
                          <Text type="secondary">来源：{importResult.source}</Text>
                        </div>
                      )}
                    </div>
                  }
                />
              </Form.Item>
            )}

            <Form.Item>
              <Space>
                <Button type="primary" onClick={handleImport} loading={loading}>
                  开始导入
                </Button>
                <Button
                  onClick={() => {
                    form.resetFields();
                    setFileList([]);
                    setImportResult(null);
                  }}
                >
                  重置
                </Button>
              </Space>
            </Form.Item>
          </Form>
        </Spin>
      </Card>
    </div>
  );
}
