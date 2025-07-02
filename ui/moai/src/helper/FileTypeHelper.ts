/**
 * 文件类型辅助类
 */
export class FileTypeHelper {
  /**
   * 获取文件类型
   * @param file 文件对象
   * @returns 文件类型
   */
  public static getFileType(file: File): string {
    // 如果文件对象已经有类型，直接返回
    if (file.type) {
      return file.type;
    }

    // 根据文件扩展名获取类型
    const extension = this.getFileExtension(file.name);
    return this.getMimeTypeByExtension(extension);
  }

  /**
   * 获取文件扩展名
   * @param fileName 文件名
   * @returns 文件扩展名（小写）
   */
  private static getFileExtension(fileName: string): string {
    const parts = fileName.split('.');
    if (parts.length > 1) {
      return parts[parts.length - 1].toLowerCase();
    }
    return '';
  }

  /**
   * 根据文件扩展名获取 MIME 类型
   * @param extension 文件扩展名
   * @returns MIME 类型
   */
  private static getMimeTypeByExtension(extension: string): string {
    const mimeTypes: { [key: string]: string } = {
      // 文档类型
      'pdf': 'application/pdf',
      'doc': 'application/msword',
      'docx': 'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
      'xls': 'application/vnd.ms-excel',
      'xlsx': 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
      'ppt': 'application/vnd.ms-powerpoint',
      'pptx': 'application/vnd.openxmlformats-officedocument.presentationml.presentation',
      'txt': 'text/plain',
      'rtf': 'application/rtf',
      'csv': 'text/csv',
      'md': 'text/markdown',

      // 图片类型
      'jpg': 'image/jpeg',
      'jpeg': 'image/jpeg',
      'png': 'image/png',
      'gif': 'image/gif',
      'bmp': 'image/bmp',
      'webp': 'image/webp',
      'svg': 'image/svg+xml',
      'ico': 'image/x-icon',

      // 音频类型
      'mp3': 'audio/mpeg',
      'wav': 'audio/wav',
      'ogg': 'audio/ogg',
      'm4a': 'audio/mp4',
      'aac': 'audio/aac',

      // 视频类型
      'mp4': 'video/mp4',
      'webm': 'video/webm',
      'avi': 'video/x-msvideo',
      'mov': 'video/quicktime',
      'wmv': 'video/x-ms-wmv',
      'flv': 'video/x-flv',

      // 压缩文件类型
      'zip': 'application/zip',
      'rar': 'application/x-rar-compressed',
      '7z': 'application/x-7z-compressed',
      'tar': 'application/x-tar',
      'gz': 'application/gzip',

      // 代码文件类型
      'js': 'application/javascript',
      'ts': 'application/typescript',
      'html': 'text/html',
      'css': 'text/css',
      'json': 'application/json',
      'xml': 'application/xml',
      'sql': 'application/sql',
      'py': 'text/x-python',
      'java': 'text/x-java-source',
      'c': 'text/x-c',
      'cpp': 'text/x-c++',
      'cs': 'text/x-csharp',
      'php': 'text/x-php',
      'rb': 'text/x-ruby',
      'go': 'text/x-go',
      'rs': 'text/x-rust',
      'swift': 'text/x-swift',
      'kt': 'text/x-kotlin',
      'scala': 'text/x-scala',
      'sh': 'text/x-shellscript',
      'bat': 'text/x-batch',
      'ps1': 'text/x-powershell',
      'yaml': 'text/yaml',
      'yml': 'text/yaml',
      'toml': 'text/toml',
      'ini': 'text/plain',
      'conf': 'text/plain',
      'log': 'text/plain',
      'env': 'text/plain',
      'gitignore': 'text/plain',
      'dockerfile': 'text/plain',
      'dockerignore': 'text/plain',
      'editorconfig': 'text/plain',
      'eslintrc': 'application/json',
      'prettierrc': 'application/json',
      'babelrc': 'application/json',
      'tsconfig': 'application/json',
      'package': 'application/json',
      'lock': 'text/plain',
      'markdown': 'text/markdown',
      'rst': 'text/x-rst',
      'tex': 'text/x-tex',
      'latex': 'text/x-latex',
      'bib': 'text/x-bibtex',
      'bst': 'text/x-bibtex',
      'cls': 'text/x-latex',
      'sty': 'text/x-latex',
      'dtx': 'text/x-latex',
      'ins': 'text/x-latex',
      'ltx': 'text/x-latex',
      'aux': 'text/x-latex',
      'out': 'text/x-latex',
      'toc': 'text/x-latex',
      'lof': 'text/x-latex',
      'lot': 'text/x-latex',
      'idx': 'text/x-latex',
      'ind': 'text/x-latex',
      'ilg': 'text/x-latex',
      'glg': 'text/x-latex',
      'glo': 'text/x-latex',
      'gls': 'text/x-latex',
      'ist': 'text/x-latex',
      'acn': 'text/x-latex',
      'acr': 'text/x-latex',
      'alg': 'text/x-latex'
    };

    return mimeTypes[extension] || 'application/octet-stream';
  }
} 