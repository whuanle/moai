# FileTypeHelper 文件类型识别帮助类

## 概述

`FileTypeHelper` 是一个用于识别和处理文件类型的工具类，特别解决了前端获取文件时 `file.type` 可能为空的问题。该类提供了多种方法来识别、验证和格式化文件类型。

## 主要功能

### 1. 基本文件类型识别

```typescript
import { FileTypeHelper } from './FileTypeHelper';

// 获取文件的 MIME 类型
const contentType = FileTypeHelper.getFileType(file);
```

### 2. 文件类型验证

```typescript
// 检查是否为知识库支持的文档类型
const isSupported = FileTypeHelper.isWikiDocumentSupported(file);

// 检查是否为图片类型
const isImage = FileTypeHelper.isImage(file);

// 检查是否为文档类型
const isDocument = FileTypeHelper.isDocument(file);

// 检查是否为文本类型
const isText = FileTypeHelper.isText(file);

// 检查是否为代码文件
const isCode = FileTypeHelper.isCode(file);
```

### 3. 文件类型描述和格式化

```typescript
// 获取文件类型描述
const description = FileTypeHelper.getFileTypeDescription(file);
// 返回: "图片文件" | "文档文件" | "文本文件" | "代码文件" | "其他文件"

// 格式化文件类型显示
const formattedType = FileTypeHelper.formatFileType(file);
// 返回: "PDF" | "JPEG" | "TXT" 等

// 获取文件扩展名（带点号）
const extension = FileTypeHelper.getFileExtensionWithDot(fileName);
// 返回: ".pdf" | ".jpg" | ".txt" 等
```

### 4. 文件类型验证

```typescript
// 验证文件类型是否匹配预期类型
const isValid = FileTypeHelper.validateFileType(file, ['pdf', 'doc', 'docx']);
```

## 支持的文件类型

### 文档类型
- PDF: `application/pdf`
- Word: `application/msword`, `application/vnd.openxmlformats-officedocument.wordprocessingml.document`
- Excel: `application/vnd.ms-excel`, `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`
- PowerPoint: `application/vnd.ms-powerpoint`, `application/vnd.openxmlformats-officedocument.presentationml.presentation`
- 文本文件: `text/plain`, `text/markdown`, `text/csv`
- RTF: `application/rtf`

### 图片类型
- JPEG: `image/jpeg`
- PNG: `image/png`
- GIF: `image/gif`
- BMP: `image/bmp`
- WebP: `image/webp`
- SVG: `image/svg+xml`
- ICO: `image/x-icon`

### 代码文件类型
- JavaScript: `application/javascript`
- TypeScript: `application/typescript`
- HTML: `text/html`
- CSS: `text/css`
- JSON: `application/json`
- XML: `application/xml`
- Python: `text/x-python`
- Java: `text/x-java-source`
- C/C++: `text/x-c`, `text/x-c++`
- 以及其他常见编程语言文件

## 使用示例

### 在文件上传组件中使用

```typescript
import { FileTypeHelper } from '../../helper/FileTypeHelper';

const handleFileSelect = (file: File) => {
  // 检查是否为支持的文档类型
  if (!FileTypeHelper.isWikiDocumentSupported(file)) {
    message.error(`${file.name} 不是支持的文档类型`);
    return false;
  }
  
  // 获取正确的文件类型
  const contentType = FileTypeHelper.getFileType(file);
  
  // 上传文件
  uploadFile(file, contentType);
  
  return false;
};
```

### 在文件列表显示中使用

```typescript
const renderFileInfo = (file: File) => {
  return (
    <div>
      <span>{file.name}</span>
      <span>{FileSizeHelper.formatFileSize(file.size)}</span>
      <span>{FileTypeHelper.formatFileType(file)}</span>
      <span>{FileTypeHelper.getFileTypeDescription(file)}</span>
    </div>
  );
};
```

### 在 API 请求中使用

```typescript
const uploadDocument = async (file: File) => {
  const client = GetApiClient();
  
  const response = await client.api.wiki.document.preupload_document.post({
    wikiId,
    contentType: FileTypeHelper.getFileType(file), // 使用帮助类获取正确的类型
    fileName: file.name,
    mD5: await GetFileMd5(file),
    fileSize: file.size,
  });
  
  return response;
};
```

## 特殊处理

### 空文件类型处理

当 `file.type` 为空或空字符串时，`FileTypeHelper` 会自动根据文件扩展名推断正确的 MIME 类型：

```typescript
// 即使 file.type 为空，也能正确识别
const file = new File([''], 'document.pdf', { type: '' });
const contentType = FileTypeHelper.getFileType(file); // 返回 "application/pdf"
```

### 未知文件类型处理

对于未知的文件类型，会返回 `application/octet-stream`：

```typescript
const file = new File([''], 'unknown.xyz', { type: '' });
const contentType = FileTypeHelper.getFileType(file); // 返回 "application/octet-stream"
```

## 知识库文档支持

`FileTypeHelper` 特别针对知识库文档上传进行了优化，支持以下文件类型：

- PDF (`.pdf`)
- Word 文档 (`.doc`, `.docx`)
- 文本文件 (`.txt`)
- Markdown 文件 (`.md`)

使用 `isWikiDocumentSupported()` 方法可以快速检查文件是否适合上传到知识库。

## 注意事项

1. **性能考虑**: 文件类型识别主要基于文件扩展名，速度很快，适合前端使用。

2. **安全性**: 仅依赖文件扩展名进行类型识别，在生产环境中建议结合服务器端验证。

3. **扩展性**: 可以通过修改 `getMimeTypeByExtension()` 方法中的 `mimeTypes` 对象来添加新的文件类型支持。

4. **兼容性**: 支持所有现代浏览器，不依赖第三方库。

## 更新日志

- **v1.0**: 基础文件类型识别功能
- **v1.1**: 添加知识库文档支持检查
- **v1.2**: 增强文件类型验证和格式化功能
- **v1.3**: 添加文件类型描述和图标支持 