// 固定分类
// 固定模板和 icon

export interface TemplateItem {
  key: string;
  name: string;
  icon: string;
  count: number;
  templates: TemplateItem[];
}

export const ClassifyList: TemplateItem[] = [
  {
    key: "tool",
    name: "工具",
    icon: "🔧",
    count: 0,
    templates: [],
  },
  {
    key: "search",
    name: "搜索",
    icon: "🔍",
    count: 0,
    templates: [],
  },
  {
    key: "multimodal",
    name: "多模态",
    icon: "🎨",
    count: 0,
    templates: [],
  },
  {
    key: "productivity",
    name: "生产力",
    icon: "⚡",
    count: 0,
    templates: [],
  },
  {
    key: "scientificresearch",
    name: "科研",
    icon: "🔬",
    count: 0,
    templates: [],
  },
  {
    key: "finance",
    name: "金融",
    icon: "💰",
    count: 0,
    templates: [],
  },
  {
    key: "design",
    name: "设计",
    icon: "🎨",
    count: 0,
    templates: [],
  },
  {
    key: "news",
    name: "新闻",
    icon: "📰",
    count: 0,
    templates: [],
  },
  {
    key: "business",
    name: "商业",
    icon: "💼",
    count: 0,
    templates: [],
  },
  {
    key: "communication",
    name: "通讯",
    icon: "📞",
    count: 0,
    templates: [],
  },
  {
    key: "social",
    name: "社交",
    icon: "👥",
    count: 0,
    templates: [],
  },
  {
    key: "ocr",
    name: "OCR",
    icon: "📄",
    count: 0,
    templates: [],
  },
  {
    key: "documentprocessing",
    name: "文档处理",
    icon: "📝",
    count: 0,
    templates: [],
  },
  {
    key: "others",
    name: "其他",
    icon: "📦",
    count: 0,
    templates: [],
  },
];
