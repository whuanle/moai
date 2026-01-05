import React, { useState, useEffect } from "react";
import { useNavigate } from "react-router";
import {
  Card,
  Table,
  Button,
  Spin,
  message,
  Tabs,
  Tag,
  Space,
  Typography,
  Modal,
  Form,
  Input,
  Select,
  InputNumber,
  Switch,
  Row,
  Col,
  Popconfirm,
  Divider,
} from "antd";
import {
  QuestionCircleOutlined,
  ReloadOutlined,
  PlusOutlined,
  InfoCircleOutlined,
  SafetyOutlined,
} from "@ant-design/icons";
import "./AiModelPage.css";
import { GetApiClient } from "../../ServiceClient";
import {
  QueryAiModelProviderListResponse,
  QueryAiModelProviderCount,
  QueryAiModelListCommandResponse,
  AiModelItem,
  AiNotKeyEndpoint,
  AiModelType,
  AiProvider,
  ModelAbilities,
  QueryAiModelListCommand,
  AiModelTypeObject,
  KeyValueBool,
} from "../../../apiClient/models";
import { RsaHelper } from "../../../helper/RsaHalper";
import { LOBE_DEFAULT_MODEL_LIST } from "../../../lobechat/packages/model-bank/aiModels";
import { ModelProvider } from "../../../lobechat/packages/model-bank/const/modelProvider";
import { proxyFormRequestError } from "../../../helper/RequestError";
import { GetServiceInfo } from "../../../InitService";

const { Title, Text } = Typography;
const { Option } = Select;

interface ModelTypeCount {
  type: AiModelType | "all";
  count: number;
}


// 获取模型类型对应的模型列表
const getModelListByType = (modelType: AiModelType) => {
  // 从 LOBE_DEFAULT_MODEL_LIST 中筛选对应类型的模型，不限制供应商
  return LOBE_DEFAULT_MODEL_LIST.filter((model) => model.type === modelType);
};

// 检查模型名称是否已存在
const isModelNameExists = (modelName: string, models: AiModelItem[]) => {
  return models.some((model) => model.name === modelName);
};

// 生成唯一的模型名称
const generateUniqueModelName = (baseName: string, models: AiModelItem[]) => {
  let name = baseName;
  let counter = 1;

  while (isModelNameExists(name, models)) {
    name = `${baseName}_${counter}`;
    counter++;
  }

  return name;
};

// 根据提供商ID映射到接口类型
const getProviderTypeFromProviderId = (providerId: string): string => {
  if (!providerId) return "openai";

  // 根据提供商的接口兼容性来映射
  const providerMap: Record<string, string> = {
    // OpenAI 使用自己的接口
    openai: "openai",

    // Anthropic 使用自己的接口
    anthropic: "anthropic",

    // Google 使用自己的接口
    google: "google",

    // Azure 使用自己的接口
    azure: "azure",
    azureai: "azure",

    // HuggingFace 使用自己的接口
    huggingface: "huggingface",

    // Ollama 使用自己的接口
    ollama: "ollama",
    ollamacloud: "ollama",
  };

  // 如果提供商的接口类型已经确定，直接返回
  if (providerMap[providerId]) {
    return providerMap[providerId];
  }

  // 默认使用 OpenAI 接口类型
  // 包括: deepseek, groq, lmstudio, cloudflare, 等等
  return "openai";
};

// 获取服务商显示名称
const getProviderDisplayName = (provider: string): string => {
  const names: Record<string, string> = {
    openai: "OpenAI",
    anthropic: "Anthropic",
    azure: "Azure",
    google: "Google",
    huggingface: "HuggingFace",
    ollama: "Ollama",
    custom: "自定义",
  };
  return names[provider] || provider;
};

// 获取所有支持的提供商列表
const getAllProviders = () => {
  return Object.values(ModelProvider).map((provider) => ({
    value: provider,
    label: provider.charAt(0).toUpperCase() + provider.slice(1).replace(/([A-Z])/g, ' $1').trim(),
    description: getProviderDescription(provider)
  }));
};

// 获取提供商描述
const getProviderDescription = (provider: string): string => {
  const descriptions: Record<string, string> = {
    ai21: "AI21 Labs，以色列人工智能公司，成立于2017年，专注于自然语言处理技术，推出Jurassic系列大语言模型，聚焦于提升文本理解与生成能力。",
    ai302: "AI302，专注于人工智能技术研发与应用的平台，提供多样化的AI模型服务，覆盖自然语言处理、图像识别等多个领域。",
    ai360: "AI360，综合性人工智能服务平台，致力于为企业和开发者提供便捷的AI模型接入方案，支持多场景智能应用开发。",
    aihubmix: "AIHubMix，人工智能模型聚合平台，整合多家供应商的优质模型资源，为用户提供一站式模型调用与管理服务。",
    akashchat: "AkashChat，专注于即时通讯场景的AI解决方案提供商，通过智能对话技术提升交互体验，支持多语言沟通场景。",
    anthropic: "Anthropic，美国人工智能公司，成立于2021年，由OpenAI前员工创立，专注于开发安全可靠的大语言模型，代表产品为Claude系列。",
    azure: "Azure，微软旗下云计算平台，提供丰富的AI服务，包括Azure OpenAI服务等，支持企业级人工智能应用部署与扩展。",
    azureai: "Azure AI，微软Azure平台旗下的人工智能服务体系，涵盖机器学习、计算机视觉、自然语言处理等全方位AI能力。",
    baichuan: "百川智能，中国人工智能公司，成立于2023年，专注于大语言模型研发，推出百川系列模型，致力于推动AGI技术落地。",
    bedrock: "AWS Bedrock，亚马逊云科技推出的全托管大语言模型服务，提供多种主流模型的统一调用接口，简化AI应用开发流程。",
    bfl: "BFL（北京前沿国际人工智能研究院），聚焦于人工智能基础研究与产业应用的科研机构，推动AI技术创新与成果转化。",
    cerebras: "Cerebras Systems，美国AI芯片与系统公司，专注于研发专用AI加速芯片，为大模型训练与推理提供高性能计算支持。",
    cloudflare: "Cloudflare，全球知名网络安全与性能优化公司，推出Workers AI服务，提供边缘计算场景下的轻量级AI模型能力。",
    cohere: "Cohere，加拿大人工智能公司，成立于2019年，专注于自然语言处理模型研发，推出Command系列模型，聚焦企业级NLP解决方案。",
    cometapi: "CometAPI，提供AI模型实验跟踪与管理的平台，帮助开发者记录、比较和优化模型训练过程，提升研发效率。",
    comfyui: "ComfyUI，开源的节点式AI图像生成工具，专注于Stable Diffusion等扩散模型的可视化工作流搭建，支持自定义图像生成流程。",
    deepseek: "深度求索（DeepSeek），成立于2023年，专注于研究世界领先的通用人工智能底层模型与技术，挑战人工智能前沿性难题。",
    fal: "Fal.ai，AI模型部署与服务平台，提供高可用的模型推理接口，支持快速将自定义模型部署为生产级API服务。",
    fireworksai: "Fireworks AI，专注于开源大模型高效部署的平台，提供优化的模型推理服务，支持多种主流开源模型的快速接入。",
    giteeai: "GiteeAI，码云（Gitee）旗下人工智能服务平台，结合代码托管生态，为开发者提供代码相关的AI辅助工具与模型服务。",
    github: "GitHub，全球最大的代码托管平台，旗下GitHub Copilot基于AI技术为开发者提供实时代码建议，提升编程效率。",
    google: "Google（谷歌），科技巨头旗下的人工智能部门，研发了Gemini等系列大模型，在自然语言处理、计算机视觉等领域处于领先地位。",
    groq: "Groq，美国AI计算公司，基于自研LPU芯片提供超高速度的模型推理服务，专注于低延迟、高吞吐量的AI计算需求。",
    higress: "Higress，阿里开源的云原生API网关，支持AI模型请求的流量管理与转发，为AI服务提供稳定的接入层支持。",
    huggingface: "Hugging Face，全球知名的开源AI社区与平台，汇聚大量预训练模型与工具，推动自然语言处理、计算机视觉等领域的开源协作。",
    hunyuan: "腾讯混元，腾讯公司自主研发的大语言模型系列，覆盖文本、图像等多模态能力，聚焦于产业级AI应用落地。",
    infiniai: "Infiniai，专注于人工智能基础模型研发的科技公司，致力于构建高效、通用的AI模型，服务于多行业智能升级。",
    internlm: "书生·浦语（InternLM），上海人工智能实验室主导研发的开源大语言模型，聚焦于中文场景优化，支持多场景应用定制。",
    jina: "Jina AI，专注于多模态人工智能的科技公司，提供从模型训练到部署的全流程工具链，支持文本、图像、音频等多模态数据处理。",
    lmstudio: "LM Studio，本地AI模型运行工具，支持用户在个人设备上部署和运行大语言模型，注重隐私保护与离线使用场景。",
    lobehub: "LobeHub，开源的AI应用开发平台，提供模型管理、交互界面设计等工具，帮助开发者快速构建AI应用。",
    minimax: "MiniMax，中国人工智能公司，成立于2021年，专注于大语言模型研发，推出abab系列模型，聚焦于对话交互场景。",
    mistral: "Mistral AI，法国人工智能公司，成立于2022年，以开源大模型为核心，推出Mistral、Mixtral等系列模型，注重模型效率与可扩展性。",
    modelscope: "ModelScope（魔搭社区），阿里达摩院推出的开源模型社区与平台，汇聚各类AI模型，提供模型训练、部署一站式服务。",
    moonshot: "月之暗面（Moonshot AI），中国人工智能公司，成立于2023年，研发了Kimi系列大语言模型，专注于长文本理解与复杂任务处理。",
    nebius: "Nebius，俄罗斯科技公司旗下的云计算与AI平台，提供模型训练与推理服务，支持多场景AI应用开发。",
    newapi: "NewAPI，AI接口聚合平台，整合多家供应商的模型资源，为用户提供标准化的API调用方式，简化多模型接入流程。",
    novita: "Novita Tech，专注于AI图像生成技术的公司，提供 Stable Diffusion 等模型的高性能推理服务，支持图像生成与编辑场景。",
    nvidia: "NVIDIA（英伟达），全球领先的GPU制造商，在AI领域提供从芯片到软件的全栈解决方案，包括NeMo大模型框架、TensorRT-LLM推理引擎等。",
    ollama: "Ollama，开源的本地大模型运行工具，支持在个人电脑上快速部署和运行LLaMA、Mistral等主流大模型，操作简单易用。",
    ollamacloud: "Ollama Cloud，Ollama推出的云端服务，提供托管的大模型推理能力，结合本地Ollama工具实现混合部署方案。",
    openai: "OpenAI，美国人工智能研究公司，成立于2015年，研发了GPT系列大语言模型，推动了生成式AI的普及，代表产品有ChatGPT等。",
    openrouter: "OpenRouter，AI模型聚合平台，整合多家供应商的大模型资源，提供统一的API接口，支持模型间的灵活切换与比较。",
    ppio: "PPIO，分布式AI算力与存储服务平台，致力于构建去中心化的AI基础设施，支持模型训练与推理的分布式部署。",
    perplexity: "Perplexity AI，专注于智能问答与信息检索的AI公司，推出PPLX系列模型，结合搜索能力提供精准的答案生成服务。",
    qiniu: "七牛云，中国云计算与数据服务公司，提供包括AI图像处理、语音识别等在内的智能服务，助力企业实现数据智能化。",
    qwen: "通义千问，阿里达摩院研发的大语言模型系列，支持多语言多模态交互，聚焦于电商、办公等产业场景的智能应用。",
    sambanova: "SambaNova Systems，美国AI计算公司，基于自研Dataflow架构提供高性能计算平台，支持大模型的高效训练与推理。",
    search1api: "Search1API，专注于搜索增强型AI服务的平台，将大语言模型与搜索引擎结合，提供精准的信息检索与生成能力。",
    sensenova: "商汤日日新，商汤科技推出的大模型体系，覆盖自然语言处理、计算机视觉等多模态能力，聚焦产业级AI解决方案。",
    siliconcloud: "SiliconCloud（硅基智能），专注于云端AI芯片与算力服务的公司，提供高效的模型推理算力，支持多场景AI应用。",
    spark: "讯飞星火，科大讯飞研发的认知大模型，具备自然语言理解、知识问答等能力，广泛应用于教育、医疗等行业。",
    stepfun: "阶跃星辰（StepFun），中国人工智能公司，专注于大语言模型与多模态技术研发，致力于为企业提供智能交互解决方案。",
    taichu: "太初，华为研发的大模型训练与推理平台，支持超大规模模型的高效训练，聚焦于AI基础能力的构建。",
    tencentcloud: "腾讯云，腾讯旗下云计算平台，提供丰富的AI服务，包括智能对话、图像识别等，支持企业级AI应用的快速部署。",
    togetherai: "Together AI，开源AI模型协作平台，提供高性能的模型训练与推理服务，支持社区开源模型的优化与部署。",
    upstage: "Upstage，韩国人工智能公司，专注于大语言模型与多模态技术研发，推出Solar系列模型，注重模型的效率与实用性。",
    v0: "V0，AI驱动的UI组件生成平台，通过自然语言描述快速生成前端UI代码，提升界面开发效率。",
    vllm: "vLLM，开源的高性能大模型推理引擎，支持PagedAttention等优化技术，显著提升模型推理吞吐量与速度。",
    vercelaigateway: "Vercel AI Gateway，Vercel推出的AI请求管理工具，提供模型请求的缓存、限流等功能，优化AI应用的性能与成本。",
    vertexai: "Google Vertex AI，谷歌云旗下的机器学习平台，提供从数据处理到模型部署的全流程服务，支持Gemini等主流模型的调用。",
    volcengine: "火山引擎，字节跳动旗下的云计算平台，提供包括豆包大模型在内的AI服务，支持多场景智能应用开发。",
    wenxin: "文心一言，百度研发的生成式大语言模型，基于ERNIE系列技术，具备文本生成、知识问答等能力，聚焦中文场景优化。",
    xai: "X.AI，埃隆·马斯克创立的人工智能公司，专注于研发安全可控的大语言模型，代表产品为Grok，注重实时信息交互能力。",
    xinference: "Xinference，开源的跨框架模型管理工具，支持多种大模型的统一部署与调用，简化多模型协作流程。",
    zeroone: "01AI（零一万物），中国人工智能公司，专注于大语言模型研发，致力于构建高效、通用的AI基础模型。",
    zhipu: "智谱AI，由清华大学团队孵化的人工智能公司，研发了GLM系列大语言模型，聚焦于自然语言处理与通用人工智能研究。",
  };
  return descriptions[provider] || `${provider} 接口格式`;
};

export default function AiModelPage() {
  const navigate = useNavigate();
  const [modelTypeCounts, setModelTypeCounts] = useState<ModelTypeCount[]>([]);
  const [models, setModels] = useState<AiModelItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [activeTab, setActiveTab] = useState<string>("all");
  const [addModalVisible, setAddModalVisible] = useState(false);
  const [editModalVisible, setEditModalVisible] = useState(false);
  const [providerListVisible, setProviderListVisible] = useState(false);
  const [currentProvider, setCurrentProvider] = useState<string>("");
  const [currentModel, setCurrentModel] = useState<AiModelItem | null>(null);
  const [form] = Form.useForm();
  const [editForm] = Form.useForm();
  const [submitting, setSubmitting] = useState(false);
  const [modelTypeKey, setModelTypeKey] = useState<string>("chat");
  const [messageApi, contextHolder] = message.useMessage();

  // 排序状态：{ field: 'name', order: 'ascend' | 'descend' | null }
  const [sortField, setSortField] = useState<string | null>(null);
  const [sortOrder, setSortOrder] = useState<'ascend' | 'descend' | null>(null);

  // 获取模型类型的中文名称
  const getModelTypeName = (type: AiModelType | "all"): string => {
    const names: Record<AiModelType | "all", string> = {
      all: "全部",
      chat: "聊天",
      embedding: "嵌入",
      image: "图像",
      tts: "语音合成",
      stts: "语音转文字",
      text2video: "文本转视频",
      text2music: "文本转音乐",
    };
    return names[type] || type;
  };

  // 获取服务商列表
  const fetchModelTypeCounts = async () => {
    try {
      const client = GetApiClient();
      const response = await client.api.admin.aimodel.providerlist.get();

      if (response?.providers) {
        // 获取所有的模型类型
        const modelTypes = Object.values(AiModelTypeObject);

        // 初始化计数
        const typeCounts: ModelTypeCount[] = [
          { type: "all", count: 0 },
          ...modelTypes.map((type) => ({ type, count: 0 })),
        ];

        // 如果有返回的providers数据，这里暂时使用总数
        // 实际应该根据providerlist返回的数据来计算
        const totalCount = response.providers.reduce(
          (sum: number, provider: any) => sum + (provider.count || 0),
          0
        );
        typeCounts[0].count = totalCount;

        setModelTypeCounts(typeCounts);
      }
    } catch (error) {
      console.error("获取模型类型统计失败:", error);
      messageApi.error("获取模型类型统计失败");
    }
  };

  // 获取所有模型列表
  const fetchModels = async () => {
    setLoading(true);
    try {
      const client = GetApiClient();

      // 构建排序字段
      const orderByFields = sortField && sortOrder ? [{
        key: sortField,
        value: sortOrder === 'ascend' // true 为升序，false 为降序
      }] : undefined;

      const requestBody: QueryAiModelListCommand = {
        aiModelType: undefined,
        isPublic: undefined,
        provider: undefined,
        orderByFields: orderByFields,
      };

      const response = await client.api.admin.aimodel.modellist.post(
        requestBody
      );

      if (response?.aiModels) {
        setModels(response.aiModels || []);

        // 重新计算每个类型的数量
        const modelTypes = Object.values(AiModelTypeObject);
        const typeCounts: ModelTypeCount[] = [
          { type: "all", count: (response.aiModels || []).length },
          ...modelTypes.map((type) => ({
            type,
            count: (response.aiModels || []).filter(
              (m: AiModelItem) => m.aiModelType === type
            ).length,
          })),
        ];

        setModelTypeCounts(typeCounts);
      }
    } catch (error) {
      console.error("获取模型列表失败:", error);
      messageApi.error("获取模型列表失败");
    } finally {
      setLoading(false);
    }
  };

  // 处理新增按钮点击
  const handleAddClick = () => {
    setCurrentProvider(""); // 清空当前provider
    form.resetFields();
    form.setFieldsValue({
      provider: undefined,
      aiModelType: "chat",
      isSystem: false,
      isPublic: false,
    });
    setAddModalVisible(true);
  };

  // 处理编辑按钮点击
  const handleEditClick = (model: AiModelItem) => {
    setCurrentModel(model);
    setCurrentProvider(model.provider || "custom");
    editForm.resetFields();
    editForm.setFieldsValue({
      aiModelId: model.id,
      name: model.name,
      title: model.title,
      provider: model.provider,
      aiModelType: model.aiModelType,
      endpoint: model.endpoint,
      deploymentName: model.deploymentName,
      contextWindowTokens: model.contextWindowTokens,
      textOutput: model.textOutput,
      maxDimension: model.maxDimension,
      vision: model.abilities?.vision || false,
      functionCall: model.abilities?.functionCall || false,
      files: model.abilities?.files || false,
      imageOutput: model.abilities?.imageOutput || false,
      isSystem: (model as any).isSystem ?? false,
      isPublic: model.isPublic ?? false,
    });
    setEditModalVisible(true);
  };

  // 处理删除按钮点击
  const handleDeleteClick = async (model: AiModelItem) => {
    try {
      const client = GetApiClient();
      await client.api.admin.aimodel.delete_model.delete({
        queryParameters: {
          aiModelId: model.id || 0,
        },
      });

      messageApi.success("模型删除成功");

      // 刷新模型列表
      await fetchModels();
    } catch (error) {
      console.error("删除模型失败:", error);
      messageApi.error("删除模型失败");
    }
  };

  // 处理模型类型变化
  const handleModelTypeChange = (modelType: AiModelType) => {
    // 清空快速配置选择和相关字段
    form.setFieldsValue({
      quickConfig: undefined,
      name: "",
      title: "",
      deploymentName: "",
      contextWindowTokens: undefined,
      textOutput: undefined,
      maxDimension: undefined,
    });

    // 更新模型类型key，触发快速配置下拉框重新渲染
    setModelTypeKey(modelType);
  };

  // 处理快速配置选择
  const handleQuickConfigChange = (modelId: string) => {
    const modelType = form.getFieldValue("aiModelType");
    const modelList = getModelListByType(modelType);
    const selectedModel = modelList.find((model) => model.id === modelId);

    if (selectedModel) {
      // 生成唯一的模型名称（基于 id）
      const uniqueName = generateUniqueModelName(selectedModel.id, models);

      // 根据模型的 providerId 自动确定提供商的接口类型
      const providerId = (selectedModel as any).providerId;
      const providerType = getProviderTypeFromProviderId(providerId);

      form.setFieldsValue({
        name: selectedModel.id, // 模型名称使用 id（确保唯一性）
        title: uniqueName, // 显示名称使用 displayName
        provider: providerType, // 自动设置接口格式
        deploymentName: selectedModel.id, // 部署名称使用 id
        contextWindowTokens: selectedModel.contextWindowTokens,
        textOutput: selectedModel.maxOutput,
        maxDimension: selectedModel.maxDimension,
        // 设置模型能力
        vision: selectedModel.abilities?.vision || false,
        functionCall: selectedModel.abilities?.functionCall || false,
        files: selectedModel.abilities?.files || false,
        imageOutput: selectedModel.abilities?.imageOutput || false,
      });
    }
  };

  // 处理新增模型提交
  const handleAddSubmit = async (values: any) => {
    setSubmitting(true);
    try {
      // 获取服务器信息并加密API密钥
      const serviceInfo = await GetServiceInfo();
      const encryptedKey = RsaHelper.encrypt(serviceInfo.rsaPublic, values.key);

      const client = GetApiClient();

      // 构建请求参数
      const requestBody = {
        name: values.name,
        title: values.title,
        provider: values.provider as AiProvider,
        aiModelType: values.aiModelType as AiModelType,
        endpoint: values.endpoint,
        key: encryptedKey, // 使用加密后的API密钥
        deploymentName: values.deploymentName,
        contextWindowTokens: values.contextWindowTokens,
        textOutput: values.textOutput,
        maxDimension: values.maxDimension,
        isPublic: values.isPublic || false, // 从表单获取isPublic值
        abilities: {
          vision: values.vision || false,
          functionCall: values.functionCall || false,
          files: values.files || false,
          imageOutput: values.imageOutput || false,
        } as ModelAbilities,
      };

      await client.api.admin.aimodel.add_aimodel.post(requestBody as any);

      messageApi.success("模型添加成功");
      setAddModalVisible(false);

      // 刷新模型列表
      await fetchModels();
    } catch (error) {
      console.error("添加模型失败:", error);
      proxyFormRequestError(error, messageApi, form);
    } finally {
      setSubmitting(false);
    }
  };

  // 处理编辑模型提交
  const handleEditSubmit = async (values: any) => {
    setSubmitting(true);
    try {
      const client = GetApiClient();

      // 构建请求参数
      const requestBody = {
        aiModelId: values.aiModelId,
        name: values.name,
        title: values.title,
        provider: values.provider as AiProvider,
        aiModelType: values.aiModelType as AiModelType,
        endpoint: values.endpoint,
        deploymentName: values.deploymentName,
        contextWindowTokens: values.contextWindowTokens,
        textOutput: values.textOutput,
        maxDimension: values.maxDimension,
        isPublic: values.isPublic ?? false,
        abilities: {
          vision: values.vision || false,
          functionCall: values.functionCall || false,
          files: values.files || false,
          imageOutput: values.imageOutput || false,
        } as ModelAbilities,
      };

      // 如果用户输入了API密钥，则加密并添加到请求中
      if (values.key && values.key.trim() !== "") {
        const serviceInfo = await GetServiceInfo();
        const encryptedKey = RsaHelper.encrypt(
          serviceInfo.rsaPublic,
          values.key
        );
        (requestBody as any).key = encryptedKey;
      } else {
        // 如果用户没有输入API密钥，则设置为空字符串
        (requestBody as any).key = "*";
      }

      await client.api.admin.aimodel.update_aimodel.post(requestBody as any);

      messageApi.success("模型更新成功");
      setEditModalVisible(false);

      // 刷新模型列表
      await fetchModels();
    } catch (error) {
      console.error("更新模型失败:", error);
      proxyFormRequestError(error, messageApi, editForm);
    } finally {
      setSubmitting(false);
    }
  };

  // 根据当前tab筛选模型
  const getFilteredModels = () => {
    if (activeTab === "all") {
      return models;
    }
    return models.filter((model) => model.aiModelType === activeTab);
  };

  // 处理表格排序变化
  const handleTableChange = (pagination: any, filters: any, sorter: any) => {
    if (sorter.field) {
      // 如果点击同一列，切换排序顺序：升序 -> 降序 -> 取消
      if (sortField === sorter.field) {
        if (sortOrder === 'ascend') {
          setSortOrder('descend');
        } else if (sortOrder === 'descend') {
          setSortField(null);
          setSortOrder(null);
        } else {
          setSortOrder('ascend');
        }
      } else {
        // 点击新列，设置为升序
        setSortField(sorter.field);
        setSortOrder('ascend');
      }
    } else {
      // 取消排序
      setSortField(null);
      setSortOrder(null);
    }
  };

  // 当排序状态改变时，重新获取数据
  useEffect(() => {
    if (models.length > 0 || sortField !== null) {
      fetchModels();
    }
  }, [sortField, sortOrder]);

  // 获取模型类型标签颜色
  const getModelTypeColor = (type: AiModelType | null) => {
    switch (type) {
      case "chat":
        return "blue";
      case "embedding":
        return "green";
      case "image":
        return "purple";
      case "tts":
        return "orange";
      case "stts":
        return "cyan";
      case "text2video":
        return "volcano";
      case "text2music":
        return "geekblue";
      default:
        return "default";
    }
  };

  // 获取能力标签
  const getAbilityTags = (abilities: any) => {
    if (!abilities) return [];

    const tags = [];
    if (abilities.vision) tags.push({ text: "视觉", color: "cyan" });
    if (abilities.functionCall)
      tags.push({ text: "函数调用", color: "magenta" });
    if (abilities.files) tags.push({ text: "文件上传", color: "geekblue" });
    if (abilities.imageOutput)
      tags.push({ text: "图像输出", color: "volcano" });

    return tags;
  };

  // 模型列表表格列定义
  const modelColumns = [
    {
      title: "模型名称",
      dataIndex: "name",
      key: "name",
      width: 180,
      sorter: true,
      sortOrder: sortField === 'name' ? sortOrder : null,
      render: (text: string) => <span className="model-name">{text}</span>,
    },
    {
      title: "显示名称",
      dataIndex: "title",
      key: "title",
      width: 150,
      sorter: true,
      sortOrder: sortField === 'title' ? sortOrder : null,
    },
    {
      title: "模型类型",
      dataIndex: "aiModelType",
      key: "aiModelType",
      width: 120,
      sorter: true,
      sortOrder: sortField === 'aiModelType' ? sortOrder : null,
      render: (type: AiModelType) => (
        <Tag color={getModelTypeColor(type)}>{getModelTypeName(type)}</Tag>
      ),
    },
    {
      title: "服务商接口",
      dataIndex: "provider",
      key: "provider",
      width: 120,
      sorter: true,
      sortOrder: sortField === 'provider' ? sortOrder : null,
      render: (provider: string) => getProviderDisplayName(provider),
    },
    {
      title: "上下文窗口",
      dataIndex: "contextWindowTokens",
      key: "contextWindowTokens",
      width: 120,
      align: "right" as const,
      render: (value: number) => (value ? `${value.toLocaleString()}` : "-"),
    },
    {
      title: "输出限制",
      dataIndex: "textOutput",
      key: "textOutput",
      width: 100,
      align: "right" as const,
      render: (value: number) => (value ? `${value.toLocaleString()}` : "-"),
    },
    {
      title: "模型能力",
      dataIndex: "abilities",
      key: "abilities",
      width: 200,
      render: (abilities: any) => (
        <Space wrap size="small">
          {getAbilityTags(abilities).map((tag, index) => (
            <Tag key={index} color={tag.color} style={{ margin: 0 }}>
              {tag.text}
            </Tag>
          ))}
        </Space>
      ),
    },
    {
      title: "端点",
      dataIndex: "endpoint",
      key: "endpoint",
      width: 200,
      ellipsis: true,
      render: (text: string) => (
        <span className="model-endpoint" title={text}>
          {text || "-"}
        </span>
      ),
    },
    {
      title: "操作",
      key: "action",
      width: 150,
      fixed: "right" as const,
      render: (_: any, record: AiModelItem) => (
        <Space size="small">
          <Button
            type="link"
            size="small"
            onClick={() => handleEditClick(record)}
          >
            编辑
          </Button>
          <Popconfirm
            title="确定要删除这个模型吗？"
            description="删除后将无法恢复"
            onConfirm={() => handleDeleteClick(record)}
            okText="确定"
            cancelText="取消"
          >
            <Button type="link" danger size="small">
              删除
            </Button>
          </Popconfirm>
        </Space>
      ),
    },
  ];

  useEffect(() => {
    fetchModels();
  }, []);

  // 准备tab项
  const tabItems = modelTypeCounts.map((count) => ({
    key: count.type,
    label: (
      <span>
        {getModelTypeName(count.type)}
        <Tag style={{ marginLeft: 8 }}>{count.count}</Tag>
      </span>
    ),
    children: (
      <div className="ai-model-table">
        <Table
          dataSource={getFilteredModels()}
          columns={modelColumns}
          rowKey={(record) => record.id?.toString() || record.name || ""}
          pagination={false}
          scroll={{ x: 1400 }}
          loading={loading}
          onChange={handleTableChange}
        />
      </div>
    ),
  }));

  return (
    <>
      {contextHolder}
      <div className="ai-model-page">
        {/* 标签页容器 - 工具栏集成在标签页右侧 */}
        <div className="ai-model-tabs-container">
          <Tabs
            activeKey={activeTab}
            onChange={setActiveTab}
            items={tabItems}
            className="ai-model-tabs"
            tabBarExtraContent={
              <Space size="middle">
                <Button
                  icon={<ReloadOutlined />}
                  onClick={() => fetchModels()}
                  loading={loading}
                >
                  刷新
                </Button>
                <Button
                  type="primary"
                  icon={<PlusOutlined />}
                  onClick={() => handleAddClick()}
                >
                  新增模型
                </Button>
                <Button
                  icon={<SafetyOutlined />}
                  onClick={() => navigate("/app/admin/modelauthorization")}
                >
                  模型授权
                </Button>
                <span
                  className="provider-link"
                  onClick={() => setProviderListVisible(true)}
                >
                  <InfoCircleOutlined /> 支持的服务商
                </span>
              </Space>
            }
          />
        </div>

        {/* 新增模型模态窗口 */}
        <Modal
          title="新增模型"
          open={addModalVisible}
          onCancel={() => setAddModalVisible(false)}
          footer={null}
          width={800}
          maskClosable={false}
          keyboard={false}
          className="ai-model-modal"
        >
          <Form form={form} layout="vertical" onFinish={handleAddSubmit}>
            {/* 第一行：模型类型和快速配置 */}
            <Row gutter={16}>
              <Col span={12}>
                <Form.Item
                  name="aiModelType"
                  label="模型类型"
                  rules={[{ required: true, message: "请选择模型类型" }]}
                >
                  <Select
                    placeholder="请选择模型类型"
                    onChange={handleModelTypeChange}
                  >
                    <Option value="chat">文本生成(对话)</Option>
                    <Option value="embedding">嵌入(向量化)</Option>
                    <Option value="image">图像生成</Option>
                    <Option value="tts">语音合成</Option>
                    <Option value="stts">语音转文字</Option>
                    <Option value="text2video">文本转视频</Option>
                    <Option value="text2music">文本转音乐</Option>
                  </Select>
                </Form.Item>
              </Col>
              <Col span={12}>
                <Form.Item name="quickConfig" label="快速配置">
                  <Form.Item
                    noStyle
                    shouldUpdate={(prevValues, currentValues) =>
                      prevValues.aiModelType !== currentValues.aiModelType
                    }
                  >
                    {({ getFieldValue }) => (
                      <Select
                        key={modelTypeKey}
                        placeholder="选择预设模型快速配置"
                        onChange={handleQuickConfigChange}
                        allowClear
                        showSearch
                        filterOption={(input, option) => {
                          if (!option?.children) return false;
                          const searchText = input.toLowerCase();
                          const optionText = option.children
                            .toString()
                            .toLowerCase();
                          return optionText.includes(searchText);
                        }}
                        optionFilterProp="children"
                      >
                        {(() => {
                          const modelType = getFieldValue("aiModelType");
                          const modelList = getModelListByType(modelType);

                          // 去重：相同ID只显示一次
                          const seenIds = new Set<string>();
                          const uniqueModels = modelList.filter((model) => {
                            if (seenIds.has(model.id)) {
                              return false;
                            }
                            seenIds.add(model.id);
                            return true;
                          });

                          // 按ID排序
                          uniqueModels.sort((a, b) => {
                            const nameA = a.id.toLowerCase();
                            const nameB = b.id.toLowerCase();
                            return nameA.localeCompare(nameB);
                          });

                          return uniqueModels.map((model) => (
                            <Option key={model.id} value={model.id}>
                              {model.id}
                            </Option>
                          ));
                        })()}
                      </Select>
                    )}
                  </Form.Item>
                </Form.Item>
              </Col>
            </Row>

            <Row gutter={16}>
              <Col span={12}>
                <Form.Item
                  name="name"
                  label="模型名称"
                  rules={[{ required: true, message: "请输入模型名称" }]}
                >
                  <Input placeholder="请输入模型名称" />
                </Form.Item>
              </Col>
              <Col span={12}>
                <Form.Item
                  name="title"
                  label="显示名称"
                  rules={[{ required: true, message: "请输入显示名称" }]}
                  extra="* 仅用于显示，以便区分用途"
                >
                  <Input placeholder="请输入显示名称" />
                </Form.Item>
              </Col>
            </Row>

            <Row gutter={16}>
              <Col span={12}>
                <Form.Item
                  name="provider"
                  label="接口格式"
                  extra="* 只用于识别不同的 API 接口格式，与具体平台无关"
                  rules={[{ required: true, message: "选择接口格式" }]}
                >
                  <Select placeholder="请选择接口格式">
                    <Option value="openai">OpenAI</Option>
                    <Option value="anthropic">Anthropic</Option>
                    <Option value="azure">Azure</Option>
                    <Option value="google">Google</Option>
                    <Option value="ollama">Ollama</Option>
                  </Select>
                </Form.Item>
              </Col>
              <Col span={12}>
                <Form.Item
                  name="deploymentName"
                  label="部署名称(Azure需填)"
                  rules={[{ required: true, message: "请输入部署名称" }]}
                  extra="* azure需要填写"
                >
                  <Input placeholder="请输入部署名称" />
                </Form.Item>
              </Col>
            </Row>
            <Row gutter={16}>
              <Col span={12}>
                <Form.Item
                  name="endpoint"
                  label="请求端点"
                  rules={[{ required: true, message: "请输入请求端点" }]}
                >
                  <Input placeholder="请输入请求端点" />
                </Form.Item>
              </Col>
              <Col span={12}>
                <Form.Item
                  name="key"
                  label="API密钥"
                  rules={[{ required: true, message: "请输入API密钥" }]}
                >
                  <Input.Password placeholder="请输入API密钥" />
                </Form.Item>
              </Col>
            </Row>

            <Row gutter={16}>
              <Col span={12}>
                <Form.Item name="contextWindowTokens" label="上下文窗口">
                  <InputNumber
                    placeholder="请输入上下文窗口大小"
                    style={{ width: "100%" }}
                    min={1}
                  />
                </Form.Item>
              </Col>
              <Col span={12}>
                <Form.Item name="textOutput" label="输出限制">
                  <InputNumber
                    placeholder="请输入输出限制"
                    style={{ width: "100%" }}
                    min={1}
                  />
                </Form.Item>
              </Col>
            </Row>

            {/* 向量维度字段，只在嵌入模型时显示 */}
            <Form.Item
              noStyle
              shouldUpdate={(prevValues, currentValues) =>
                prevValues.aiModelType !== currentValues.aiModelType
              }
            >
              {({ getFieldValue }) => {
                const modelType = getFieldValue("aiModelType");
                if (modelType === "embedding") {
                  return (
                    <Row gutter={16}>
                      <Col span={12}>
                        <Form.Item
                          name="maxDimension"
                          label="向量维度"
                          rules={[
                            { required: true, message: "请输入向量维度" },
                          ]}
                        >
                          <InputNumber
                            placeholder="请输入向量维度"
                            style={{ width: "100%" }}
                            min={1}
                          />
                        </Form.Item>
                      </Col>
                    </Row>
                  );
                }
                return null;
              }}
            </Form.Item>

            {/* 系统设置 */}
            <Row gutter={16}>
              <Col span={12}>
                <Form.Item
                  name="isPublic"
                  label="公开使用"
                  valuePropName="checked"
                  extra="* 开放给所有人使用"
                >
                  <Switch checkedChildren="是" unCheckedChildren="否" />
                </Form.Item>
              </Col>
            </Row>

            {/* 模型能力 - 仅 chat 类型显示 */}
            <Form.Item
              noStyle
              shouldUpdate={(prevValues, currentValues) =>
                prevValues.aiModelType !== currentValues.aiModelType
              }
            >
              {({ getFieldValue }) => {
                const modelType = getFieldValue("aiModelType");
                if (modelType === "chat") {
                  return (
                    <Form.Item label="模型能力">
                      <Row gutter={16}>
                        <Col span={6}>
                          <Form.Item name="vision" valuePropName="checked">
                            <Switch checkedChildren="视觉" unCheckedChildren="视觉" />
                          </Form.Item>
                        </Col>
                        <Col span={6}>
                          <Form.Item name="functionCall" valuePropName="checked">
                            <Switch
                              checkedChildren="函数调用"
                              unCheckedChildren="函数调用"
                            />
                          </Form.Item>
                        </Col>
                        <Col span={6}>
                          <Form.Item name="files" valuePropName="checked">
                            <Switch
                              checkedChildren="文件上传"
                              unCheckedChildren="文件上传"
                            />
                          </Form.Item>
                        </Col>
                        <Col span={6}>
                          <Form.Item name="imageOutput" valuePropName="checked">
                            <Switch
                              checkedChildren="图像输出"
                              unCheckedChildren="图像输出"
                            />
                          </Form.Item>
                        </Col>
                      </Row>
                    </Form.Item>
                  );
                }
                return null;
              }}
            </Form.Item>

            <Form.Item style={{ marginTop: "24px", textAlign: "right" }}>
              <Space>
                <Button onClick={() => setAddModalVisible(false)}>取消</Button>
                <Button type="primary" htmlType="submit" loading={submitting}>
                  确定
                </Button>
              </Space>
            </Form.Item>
          </Form>
        </Modal>

        {/* 编辑模型模态窗口 */}
        <Modal
          title="编辑模型"
          open={editModalVisible}
          onCancel={() => setEditModalVisible(false)}
          footer={null}
          width={800}
          maskClosable={false}
          keyboard={false}
          className="ai-model-modal"
        >
          <Form form={editForm} layout="vertical" onFinish={handleEditSubmit}>
            <Form.Item name="aiModelId" hidden>
              <Input />
            </Form.Item>

            <Row gutter={16}>
              <Col span={12}>
                <Form.Item
                  name="aiModelType"
                  label="模型类型"
                  rules={[{ required: true, message: "请选择模型类型" }]}
                >
                  <Select placeholder="请选择模型类型" disabled>
                    <Option value="chat">聊天</Option>
                    <Option value="embedding">嵌入</Option>
                    <Option value="image">图像</Option>
                    <Option value="tts">语音合成</Option>
                    <Option value="stt">语音转文字</Option>
                    <Option value="realtime">实时</Option>
                    <Option value="text2video">文本转视频</Option>
                    <Option value="text2music">文本转音乐</Option>
                  </Select>
                </Form.Item>
              </Col>
              <Col span={12}>
                <Form.Item
                  name="provider"
                  label="服务商接口类型"
                  rules={[{ required: true, message: "请选择服务商接口类型" }]}
                >
                  <Select placeholder="请选择服务商接口类型">
                    <Option value="openai">OpenAI</Option>
                    <Option value="anthropic">Anthropic</Option>
                    <Option value="azure">Azure</Option>
                    <Option value="google">Google</Option>
                    <Option value="ollama">Ollama</Option>
                  </Select>
                </Form.Item>
              </Col>
            </Row>

            <Row gutter={16}>
              <Col span={12}>
                <Form.Item
                  name="name"
                  label="模型名称"
                  rules={[{ required: true, message: "请输入模型名称" }]}
                >
                  <Input placeholder="请输入模型名称" />
                </Form.Item>
              </Col>
              <Col span={12}>
                <Form.Item
                  name="title"
                  label="显示名称"
                  rules={[{ required: true, message: "请输入显示名称" }]}
                  extra="* 仅用于显示，以便区分用途"
                >
                  <Input placeholder="请输入显示名称" />
                </Form.Item>
              </Col>
            </Row>

            <Row gutter={16}>
              <Col span={12}>
                <Form.Item
                  name="endpoint"
                  label="请求端点"
                  rules={[{ required: true, message: "请输入请求端点" }]}
                >
                  <Input placeholder="请输入请求端点" />
                </Form.Item>
              </Col>
              <Col span={12}>
                <Form.Item
                  name="deploymentName"
                  label="部署名称"
                  rules={[{ required: true, message: "请输入部署名称" }]}
                  extra="* azure需要填写"
                >
                  <Input placeholder="请输入部署名称" />
                </Form.Item>
              </Col>
            </Row>

            <Row gutter={16}>
              <Col span={12}>
                <Form.Item
                  name="key"
                  label="API密钥"
                  extra="* 留空则不更新API密钥"
                >
                  <Input.Password placeholder="请输入API密钥（留空则不更新）" />
                </Form.Item>
              </Col>
            </Row>

            <Row gutter={16}>
              <Col span={12}>
                <Form.Item name="contextWindowTokens" label="上下文窗口">
                  <InputNumber
                    placeholder="请输入上下文窗口大小"
                    style={{ width: "100%" }}
                    min={1}
                  />
                </Form.Item>
              </Col>
              <Col span={12}>
                <Form.Item name="textOutput" label="输出限制">
                  <InputNumber
                    placeholder="请输入输出限制"
                    style={{ width: "100%" }}
                    min={1}
                  />
                </Form.Item>
              </Col>
            </Row>

            {/* 向量维度字段，只在嵌入模型时显示 */}
            <Form.Item
              noStyle
              shouldUpdate={(prevValues, currentValues) =>
                prevValues.aiModelType !== currentValues.aiModelType
              }
            >
              {({ getFieldValue }) => {
                const modelType = getFieldValue("aiModelType");
                if (modelType === "embedding") {
                  return (
                    <Row gutter={16}>
                      <Col span={12}>
                        <Form.Item
                          name="maxDimension"
                          label="向量维度"
                          rules={[
                            { required: true, message: "请输入向量维度" },
                          ]}
                        >
                          <InputNumber
                            placeholder="请输入向量维度"
                            style={{ width: "100%" }}
                            min={1}
                          />
                        </Form.Item>
                      </Col>
                    </Row>
                  );
                }
                return null;
              }}
            </Form.Item>

            {/* 系统设置 */}
            <Row gutter={16}>
              <Col span={12}>
                <Form.Item
                  name="isPublic"
                  label="公开使用"
                  valuePropName="checked"
                  extra="* 公开给所有人使用"
                >
                  <Switch checkedChildren="是" unCheckedChildren="否" />
                </Form.Item>
              </Col>
            </Row>

            {/* 模型能力 - 仅 chat 类型显示 */}
            <Form.Item
              noStyle
              shouldUpdate={(prevValues, currentValues) =>
                prevValues.aiModelType !== currentValues.aiModelType
              }
            >
              {({ getFieldValue }) => {
                const modelType = getFieldValue("aiModelType");
                if (modelType === "chat") {
                  return (
                    <Form.Item label="模型能力">
                      <Row gutter={16}>
                        <Col span={6}>
                          <Form.Item name="vision" valuePropName="checked">
                            <Switch checkedChildren="视觉" unCheckedChildren="视觉" />
                          </Form.Item>
                        </Col>
                        <Col span={6}>
                          <Form.Item name="functionCall" valuePropName="checked">
                            <Switch
                              checkedChildren="函数调用"
                              unCheckedChildren="函数调用"
                            />
                          </Form.Item>
                        </Col>
                        <Col span={6}>
                          <Form.Item name="files" valuePropName="checked">
                            <Switch
                              checkedChildren="文件上传"
                              unCheckedChildren="文件上传"
                            />
                          </Form.Item>
                        </Col>
                        <Col span={6}>
                          <Form.Item name="imageOutput" valuePropName="checked">
                            <Switch
                              checkedChildren="图像输出"
                              unCheckedChildren="图像输出"
                            />
                          </Form.Item>
                        </Col>
                      </Row>
                    </Form.Item>
                  );
                }
                return null;
              }}
            </Form.Item>

            <Form.Item style={{ marginTop: "24px", textAlign: "right" }}>
              <Space>
                <Button onClick={() => setEditModalVisible(false)}>取消</Button>
                <Button type="primary" htmlType="submit" loading={submitting}>
                  确定
                </Button>
              </Space>
            </Form.Item>
          </Form>
        </Modal>

        {/* 支持的提供商列表 */}
        <Modal
          title="支持的提供商列表"
          open={providerListVisible}
          onCancel={() => setProviderListVisible(false)}
          footer={null}
          width={700}
          maskClosable={false}
          className="ai-model-modal provider-list-modal"
        >
          <div className="provider-list-description">
            本系统支持以下提供商的接口格式，可根据实际使用的服务商选择对应的接口类型
          </div>
          <Space direction="vertical" style={{ width: "100%" }} size="middle">
            {getAllProviders().map((provider) => (
              <Card
                key={provider.value}
                size="small"
                hoverable
                className="provider-card"
              >
                <div className="provider-name">{provider.label}</div>
                <div className="provider-description">{provider.description}</div>
              </Card>
            ))}
          </Space>
        </Modal>
      </div>
    </>
  );
}
