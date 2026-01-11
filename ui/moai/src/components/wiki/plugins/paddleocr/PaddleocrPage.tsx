import { useState, useEffect, useCallback, useMemo } from 'react';
import { useParams } from 'react-router';
import {
    Button, Card, Select, Upload, message, Spin, Typography, Space, Input, Row, Col, Image, Switch, Divider
} from 'antd';
import {
    UploadOutlined, EyeOutlined, ImportOutlined, ReloadOutlined
} from '@ant-design/icons';
import type { UploadFile, UploadProps, RcFile } from 'antd/es/upload/interface';
import { GetApiClient } from '../../../ServiceClient';
import {
    QueryPaddleocrPluginListCommand,
    QueryPaddleocrPluginListCommandResponse,
    PaddleocrPluginItem,
    PreviewPaddleocrDocumentCommand,
    PaddleocrPreviewResult,
    ImportPaddleocrDocumentCommand,
    PreUploadTempFileCommand,
} from '../../../../apiClient/models';
import { proxyRequestError } from '../../../../helper/RequestError';
import { GetFileMd5 } from '../../../../helper/Md5Helper';
import { FileTypeHelper } from '../../../../helper/FileTypeHelper';
import '../../../../styles/theme.css';
import './PaddleocrPage.css';

const { Text } = Typography;
const { TextArea } = Input;

// 文件类型选项
const FILE_TYPE_OPTIONS = [
    { value: 0, label: 'PDF 文档' },
    { value: 1, label: '图像文件' },
];

// 插件参数配置定义
interface PluginParamConfig {
    key: string;
    label: string;
    description: string;
    defaultValue: boolean;
}

// 不同插件模板的参数配置
const PLUGIN_PARAMS: Record<string, PluginParamConfig[]> = {
    paddleocr_ocr: [
        { key: 'useDocOrientationClassify', label: '文档方向分类', description: '是否使用文档方向分类', defaultValue: false },
        { key: 'useDocUnwarping', label: '文本图像矫正', description: '是否使用文本图像矫正', defaultValue: false },
        { key: 'useTextlineOrientation', label: '文本行方向分类', description: '是否使用文本行方向分类', defaultValue: false },
    ],
    paddleocr_structure_v3: [
        { key: 'useDocOrientationClassify', label: '文档方向分类', description: '是否使用文档方向分类', defaultValue: false },
        { key: 'useDocUnwarping', label: '文本图像矫正', description: '是否使用文本图像矫正', defaultValue: false },
        { key: 'useTableRecognition', label: '表格识别', description: '是否使用表格识别', defaultValue: false },
        { key: 'useFormulaRecognition', label: '公式识别', description: '是否使用公式识别', defaultValue: false },
        { key: 'useSealRecognition', label: '印章识别', description: '是否使用印章识别', defaultValue: false },
        { key: 'useChartRecognition', label: '图表解析', description: '是否使用图表解析', defaultValue: false },
        { key: 'useRegionDetection', label: '文档区域检测', description: '是否使用文档区域检测', defaultValue: false },
    ],
    paddleocr_vl: [
        { key: 'useDocOrientationClassify', label: '文档方向分类', description: '是否使用文档方向分类', defaultValue: false },
        { key: 'useDocUnwarping', label: '文本图像矫正', description: '是否使用文本图像矫正', defaultValue: false },
        { key: 'useLayoutDetection', label: '版面区域检测排序', description: '是否使用版面区域检测排序', defaultValue: false },
        { key: 'useChartRecognition', label: '图表解析', description: '是否使用图表解析', defaultValue: false },
        { key: 'prettifyMarkdown', label: '美化 Markdown', description: '是否输出美化后的 Markdown', defaultValue: false },
        { key: 'showFormulaNumber', label: '公式编号', description: 'Markdown 中是否包含公式编号', defaultValue: false },
    ],
};

export default function PaddleocrPage() {
    const { id } = useParams<{ id: string }>();
    const wikiId = id ? parseInt(id) : undefined;

    const [messageApi, contextHolder] = message.useMessage();

    // 插件列表
    const [plugins, setPlugins] = useState<PaddleocrPluginItem[]>([]);
    const [pluginsLoading, setPluginsLoading] = useState(false);
    const [selectedPluginId, setSelectedPluginId] = useState<number | null>(null);

    // 文件上传
    const [fileList, setFileList] = useState<UploadFile[]>([]);
    const [fileType, setFileType] = useState<number>(0);
    const [uploadedFileId, setUploadedFileId] = useState<number | null>(null);
    const [uploading, setUploading] = useState(false);

    // 插件参数配置
    const [pluginConfig, setPluginConfig] = useState<Record<string, boolean>>({});

    // 预览结果
    const [previewLoading, setPreviewLoading] = useState(false);
    const [previewResult, setPreviewResult] = useState<PaddleocrPreviewResult | null>(null);
    const [editedTexts, setEditedTexts] = useState<string[]>([]);

    // 导入
    const [importLoading, setImportLoading] = useState(false);
    const [documentTitle, setDocumentTitle] = useState<string>('');

    // 获取当前选中插件的模板 key
    const selectedPlugin = useMemo(() => {
        return plugins.find(p => p.pluginId === selectedPluginId);
    }, [plugins, selectedPluginId]);

    // 获取当前插件的参数配置列表
    const currentPluginParams = useMemo(() => {
        const templateKey = selectedPlugin?.templatePluginKey;
        if (!templateKey) return [];
        return PLUGIN_PARAMS[templateKey] || [];
    }, [selectedPlugin]);

    // 当选择的插件变化时，重置参数配置
    useEffect(() => {
        const defaultConfig: Record<string, boolean> = {};
        currentPluginParams.forEach(param => {
            defaultConfig[param.key] = param.defaultValue;
        });
        setPluginConfig(defaultConfig);
    }, [currentPluginParams]);

    // 获取插件列表
    const fetchPluginList = useCallback(async () => {
        if (!wikiId) return;

        setPluginsLoading(true);
        try {
            const client = GetApiClient();
            const requestBody: QueryPaddleocrPluginListCommand = {
                wikiId: wikiId,
            };

            const response: QueryPaddleocrPluginListCommandResponse | undefined =
                await client.api.wiki.plugin.paddleocr.plugin_list.post(requestBody);

            if (response?.items) {
                setPlugins(response.items);
                // 默认选中第一个插件
                if (response.items.length > 0 && !selectedPluginId) {
                    setSelectedPluginId(response.items[0].pluginId || null);
                }
            } else {
                setPlugins([]);
            }
        } catch (error) {
            console.error('获取插件列表失败:', error);
            proxyRequestError(error, messageApi, '获取插件列表失败');
        } finally {
            setPluginsLoading(false);
        }
    }, [wikiId, messageApi, selectedPluginId]);

    useEffect(() => {
        if (wikiId) {
            fetchPluginList();
        }
    }, [wikiId, fetchPluginList]);

    // 上传文件到服务器
    const uploadFile = async (file: RcFile): Promise<number | null> => {
        try {
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
        } catch (error) {
            console.error('文件上传失败:', error);
            throw error;
        }
    };

    // 文件上传配置
    const uploadProps: UploadProps = {
        beforeUpload: async (file: RcFile) => {
            // 检查文件类型
            const isPdf = file.type === 'application/pdf';
            const isImage = file.type.startsWith('image/');

            if (fileType === 0 && !isPdf) {
                messageApi.error('请上传 PDF 文件');
                return Upload.LIST_IGNORE;
            }
            if (fileType === 1 && !isImage) {
                messageApi.error('请上传图像文件');
                return Upload.LIST_IGNORE;
            }

            setUploading(true);
            try {
                const fileId = await uploadFile(file);
                if (fileId) {
                    setUploadedFileId(fileId);
                    setFileList([{
                        uid: file.uid,
                        name: file.name,
                        status: 'done',
                    }]);
                    messageApi.success('文件上传成功');
                }
            } catch (error) {
                console.error('文件上传失败:', error);
                proxyRequestError(error, messageApi, '文件上传失败');
            } finally {
                setUploading(false);
            }

            return false; // 阻止默认上传行为
        },
        fileList,
        maxCount: 1,
        onRemove: () => {
            setFileList([]);
            setUploadedFileId(null);
            setPreviewResult(null);
            setEditedTexts([]);
        },
    };


    // 更新参数配置
    const handleConfigChange = (key: string, value: boolean) => {
        setPluginConfig(prev => ({
            ...prev,
            [key]: value,
        }));
    };

    // 预览 OCR 结果
    const handlePreview = async () => {
        if (!wikiId || !selectedPluginId || !uploadedFileId) {
            messageApi.warning('请先选择插件并上传文件');
            return;
        }

        setPreviewLoading(true);
        setPreviewResult(null);
        setEditedTexts([]);

        try {
            const client = GetApiClient();

            // 构建配置 JSON，包含 fileType
            const configJson = JSON.stringify({
                ...pluginConfig,
                fileType: fileType,
            });

            const requestBody: PreviewPaddleocrDocumentCommand = {
                wikiId: wikiId,
                pluginId: selectedPluginId,
                fileId: uploadedFileId,
                config: configJson,
            };

            const response: PaddleocrPreviewResult | undefined =
                await client.api.wiki.plugin.paddleocr.preview.post(requestBody);

            if (response) {
                setPreviewResult(response);
                setEditedTexts(response.texts || []);
                messageApi.success('OCR 解析完成');
            }
        } catch (error) {
            console.error('OCR 预览失败:', error);
            proxyRequestError(error, messageApi, 'OCR 预览失败');
        } finally {
            setPreviewLoading(false);
        }
    };

    // 导入文档
    const handleImport = async () => {
        if (!wikiId || editedTexts.length === 0) {
            messageApi.warning('请先进行 OCR 解析');
            return;
        }

        if (!documentTitle.trim()) {
            messageApi.warning('请输入文档标题');
            return;
        }

        setImportLoading(true);
        try {
            const client = GetApiClient();
            // 将文本数组合并为 markdown
            const markdownContent = editedTexts.join('\n\n');
            const requestBody: ImportPaddleocrDocumentCommand = {
                wikiId: wikiId,
                title: documentTitle.trim(),
                markdownContent: markdownContent,
            };

            await client.api.wiki.plugin.paddleocr.importEscaped.post(requestBody);
            messageApi.success('文档导入成功');

            // 只清空标题，保留解析结果
            setDocumentTitle('');
        } catch (error) {
            console.error('导入文档失败:', error);
            proxyRequestError(error, messageApi, '导入文档失败');
        } finally {
            setImportLoading(false);
        }
    };

    // 重置
    const handleReset = () => {
        setPreviewResult(null);
        setEditedTexts([]);
        setDocumentTitle('');
        setFileList([]);
        setUploadedFileId(null);
    };

    // 更新单个文本
    const handleTextChange = (index: number, value: string) => {
        setEditedTexts(prev => {
            const newTexts = [...prev];
            newTexts[index] = value;
            return newTexts;
        });
    };

    if (!wikiId) {
        return (
            <div className="page-container">
                <div className="moai-empty">
                    <Text type="secondary">缺少知识库ID</Text>
                </div>
            </div>
        );
    }

    return (
        <div className="page-container">
            {contextHolder}

            {/* 页面标题区域 */}
            <div className="moai-page-header">
                <h1 className="moai-page-title">飞桨 OCR 文档导入</h1>
                <p className="moai-page-subtitle">
                    使用飞桨 OCR 插件解析 PDF 或图像文件，将识别结果导入知识库
                </p>
            </div>

            {/* 配置区域 */}
            <Card className="paddleocr-config-card">
                <Row gutter={[24, 16]}>
                    <Col xs={24} md={8}>
                        <div className="paddleocr-form-item">
                            <div className="paddleocr-form-label">
                                <Text strong>选择 OCR 插件</Text>
                                <Button
                                    type="text"
                                    size="small"
                                    icon={<ReloadOutlined />}
                                    onClick={fetchPluginList}
                                    loading={pluginsLoading}
                                />
                            </div>
                            <Select
                                placeholder="请选择 OCR 插件"
                                value={selectedPluginId}
                                onChange={setSelectedPluginId}
                                loading={pluginsLoading}
                                optionLabelProp="label"
                                options={plugins.map(p => ({
                                    value: p.pluginId,
                                    label: p.title || p.pluginName,
                                }))}
                                optionRender={(option) => {
                                    const plugin = plugins.find(p => p.pluginId === option.value);
                                    return (
                                        <div className="paddleocr-plugin-option">
                                            <div>{plugin?.title || plugin?.pluginName}</div>
                                            {plugin?.description && (
                                                <Text type="secondary" className="paddleocr-plugin-desc">
                                                    {plugin.description}
                                                </Text>
                                            )}
                                        </div>
                                    );
                                }}
                                notFoundContent={
                                    pluginsLoading ? <Spin size="small" /> : '暂无可用插件'
                                }
                            />
                        </div>
                    </Col>

                    <Col xs={24} md={8}>
                        <div className="paddleocr-form-item">
                            <div className="paddleocr-form-label">
                                <Text strong>文件类型</Text>
                            </div>
                            <Select
                                value={fileType}
                                onChange={(value) => {
                                    setFileType(value);
                                    setFileList([]);
                                    setUploadedFileId(null);
                                    setPreviewResult(null);
                                }}
                                options={FILE_TYPE_OPTIONS}
                            />
                        </div>
                    </Col>

                    <Col xs={24} md={8}>
                        <div className="paddleocr-form-item">
                            <div className="paddleocr-form-label">
                                <Text strong>上传文件</Text>
                            </div>
                            <Upload {...uploadProps}>
                                <Button
                                    icon={<UploadOutlined />}
                                    disabled={!selectedPluginId}
                                    loading={uploading}
                                >
                                    {fileType === 0 ? '选择 PDF 文件' : '选择图像文件'}
                                </Button>
                            </Upload>
                        </div>
                    </Col>
                </Row>

                {/* 插件参数配置 */}
                {currentPluginParams.length > 0 && (
                    <>
                        <Divider orientation="left">
                            <Text type="secondary">OCR 参数配置</Text>
                        </Divider>
                        <Row gutter={[16, 12]}>
                            {currentPluginParams.map(param => (
                                <Col xs={24} sm={12} md={8} key={param.key}>
                                    <div className="paddleocr-param-item">
                                        <Switch
                                            checked={pluginConfig[param.key] || false}
                                            onChange={(checked) => handleConfigChange(param.key, checked)}
                                            size="small"
                                        />
                                        <div className="paddleocr-param-info">
                                            <Text>{param.label}</Text>
                                            <Text type="secondary" className="paddleocr-param-desc">
                                                {param.description}
                                            </Text>
                                        </div>
                                    </div>
                                </Col>
                            ))}
                        </Row>
                    </>
                )}

                <div className="paddleocr-actions">
                    <Space>
                        <Button
                            type="primary"
                            icon={<EyeOutlined />}
                            onClick={handlePreview}
                            loading={previewLoading}
                            disabled={!selectedPluginId || !uploadedFileId}
                        >
                            开始 OCR 解析
                        </Button>
                        <Button onClick={handleReset}>
                            重置
                        </Button>
                    </Space>
                </div>
            </Card>

            {/* 预览结果区域 */}
            {(previewLoading || previewResult) && (
                <Card
                    title="OCR 解析结果"
                    className="paddleocr-result-card"
                    loading={previewLoading}
                >
                    {previewResult && (
                        <>
                            {/* 导入配置 */}
                            <div className="paddleocr-import-section-top">
                                <Row gutter={16} align="middle">
                                    <Col flex="auto">
                                        <Input
                                            placeholder="请输入文档标题"
                                            value={documentTitle}
                                            onChange={(e) => setDocumentTitle(e.target.value)}
                                            addonBefore="文档标题"
                                        />
                                    </Col>
                                    <Col>
                                        <Button
                                            type="primary"
                                            icon={<ImportOutlined />}
                                            onClick={handleImport}
                                            loading={importLoading}
                                            disabled={editedTexts.length === 0 || !documentTitle.trim()}
                                        >
                                            导入到知识库
                                        </Button>
                                    </Col>
                                </Row>
                            </div>

                            {/* 左右分栏显示文本和图片 */}
                            <div className="paddleocr-result-list">
                                {editedTexts.map((text, index) => (
                                    <div key={index} className="paddleocr-result-item">
                                        <div className="paddleocr-result-index">
                                            <Text type="secondary">#{index + 1}</Text>
                                        </div>
                                        <div className="paddleocr-result-content">
                                            <div className="paddleocr-result-text">
                                                <TextArea
                                                    value={text}
                                                    onChange={(e) => handleTextChange(index, e.target.value)}
                                                    autoSize={{ minRows: 3, maxRows: 10 }}
                                                    placeholder="识别的文本内容"
                                                />
                                            </div>
                                            <div className="paddleocr-result-image">
                                                {previewResult.images && previewResult.images[index] ? (
                                                    <Image
                                                        src={previewResult.images[index]}
                                                        alt={`OCR 结果 ${index + 1}`}
                                                        className="paddleocr-preview-image"
                                                    />
                                                ) : (
                                                    <div className="paddleocr-no-image">
                                                        <Text type="secondary">无图片</Text>
                                                    </div>
                                                )}
                                            </div>
                                        </div>
                                    </div>
                                ))}
                            </div>
                        </>
                    )}
                </Card>
            )}

            {/* 空状态提示 */}
            {!previewLoading && !previewResult && (
                <Card className="paddleocr-empty-card">
                    <div className="moai-empty">
                        <Text type="secondary">
                            请选择 OCR 插件并上传文件，然后点击"开始 OCR 解析"
                        </Text>
                    </div>
                </Card>
            )}
        </div>
    );
}
