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
} from 'antd';
import { UploadOutlined, LinkOutlined } from '@ant-design/icons';
import type { UploadFile, RcFile } from 'antd/es/upload/interface';
import { GetApiClient } from '../../../ServiceClient';
import { proxyFormRequestError } from '../../../../helper/RequestError';
import { GetFileMd5 } from '../../../../helper/Md5Helper';
import { FileTypeHelper } from '../../../../helper/FileTypeHelper';

const { Text, Title } = Typography;

type ImportType = 'url' | 'file';

export default function OpenApiListPage() {
  const { id } = useParams<{ id: string }>();
  const wikiId = id ? parseInt(id) : undefined;
  
  const [loading, setLoading] = useState(false);
  const [messageApi, contextHolder] = message.useMessage();
  const [form] = Form.useForm();
  const [importType, setImportType] = useState<ImportType>('url');
  const [fileList, setFileList] = useState<UploadFile[]>([]);

  // 上传文件到服务器
  const uploadFile = async (file: RcFile): Promise<number | null> => {
    try {
      const client = GetApiClient();
      const md5 = await GetFileMd5(file);
      
      // 预上传获取签名URL
      const preUploadResponse = await client.api.wiki.document.preupload_document.post({
        wikiId: wikiId,
        contentType: FileTypeHelper.getFileType(file),
        fileName: file.name,
        mD5: md5,
        fileSize: file.size,
      });

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
          'x-amz-meta-max-file-size': file.size.toString(),
          'Authorization': `Bearer ${localStorage.getItem('userinfo.accessToken')}`,
        },
      });

      if (uploadResponse.status !== 200) {
        throw new Error(uploadResponse.statusText);
      }

      return preUploadResponse.fileId || null;
    } catch (error) {
      console.error('文件上传失败:', error);
      throw error;
    }
  };

  // 处理导入
  const handleImport = useCallback(async () => {
    if (!wikiId) {
      messageApi.error('缺少知识库ID');
      return;
    }

    try {
      await form.validateFields();
      const values = form.getFieldsValue();
      
      setLoading(true);
      const client = GetApiClient();

      let requestBody: {
        wikiId: number;
        url?: string | null;
        fileId?: number | null;
      } = {
        wikiId: wikiId,
      };

      if (importType === 'url') {
        // URL 导入
        if (!values.url?.trim()) {
          messageApi.error('请输入 OpenAPI 文档 URL');
          return;
        }
        requestBody.url = values.url.trim();
      } else {
        // 文件导入
        if (fileList.length === 0) {
          messageApi.error('请选择要上传的文件');
          return;
        }
        
        const file = fileList[0].originFileObj as RcFile;
        const fileId = await uploadFile(file);
        
        if (!fileId) {
          messageApi.error('文件上传失败');
          return;
        }
        
        requestBody.fileId = fileId;
      }

      await client.api.wiki.document.import_api.post(requestBody);
      messageApi.success('OpenAPI 文档导入成功');
      
      // 重置表单
      form.resetFields();
      setFileList([]);
    } catch (error) {
      console.error('导入失败:', error);
      proxyFormRequestError(error, messageApi, form, '导入失败');
    } finally {
      setLoading(false);
    }
  }, [wikiId, importType, fileList, form, messageApi]);

  // 文件选择前的校验
  const beforeUpload = (file: RcFile) => {
    const isValidType = file.name.endsWith('.json') || 
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
    
    return false; // 阻止自动上传
  };

  // 文件列表变化
  const handleFileChange = ({ fileList: newFileList }: { fileList: UploadFile[] }) => {
    setFileList(newFileList.slice(-1)); // 只保留最后一个文件
  };

  if (!wikiId) {
    return (
      <Card>
        <Text type="secondary">缺少知识库ID</Text>
      </Card>
    );
  }

  return (
    <div>
      {contextHolder}
      
      <Card>
        <Title level={4}>OpenAPI 文档导入</Title>
        <Text type="secondary">
          导入 OpenAPI (Swagger) 规范文档，系统将自动解析 API 接口信息并生成知识库文档。
        </Text>
      </Card>

      <Card style={{ marginTop: 16 }}>
        <Spin spinning={loading}>
          <Form
            form={form}
            layout="vertical"
            style={{ maxWidth: 600 }}
          >
            <Form.Item label="导入方式">
              <Radio.Group 
                value={importType} 
                onChange={(e) => setImportType(e.target.value)}
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
                />
              </Form.Item>
            ) : (
              <Form.Item
                label="上传文件"
                required
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
                <Text type="secondary" style={{ display: 'block', marginTop: 8 }}>
                  支持 JSON、YAML 格式，文件大小不超过 10MB
                </Text>
              </Form.Item>
            )}

            <Form.Item>
              <Space>
                <Button 
                  type="primary" 
                  onClick={handleImport}
                  loading={loading}
                >
                  开始导入
                </Button>
                <Button 
                  onClick={() => {
                    form.resetFields();
                    setFileList([]);
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
