export class FileSizeHelper {
    private static readonly UNITS = ['B', 'KB', 'MB', 'GB', 'TB', 'PB'];
    private static readonly BASE = 1024;

    /**
     * Convert file size in bytes to human readable format
     * @param bytes File size in bytes
     * @param decimals Number of decimal places to show (default: 2)
     * @returns Formatted string like "1.5 MB"
     */
    public static formatFileSize(bytes: number, decimals: number = 2): string {
        if (bytes === 0 || bytes === undefined || bytes === null) return '0 B';

        const k = this.BASE;
        const dm = decimals < 0 ? 0 : decimals;
        const i = Math.floor(Math.log(bytes) / Math.log(k));

        return `${parseFloat((bytes / Math.pow(k, i)).toFixed(dm))} ${this.UNITS[i]}`;
    }

    /**
     * Convert human readable file size back to bytes
     * @param sizeString Formatted string like "1.5 MB"
     * @returns Size in bytes
     */
    public static parseFileSize(sizeString: string): number {
        const match = sizeString.match(/^([\d.]+)\s*([A-Za-z]+)$/);
        if (!match) return 0;

        const [, size, unit] = match;
        const unitIndex = this.UNITS.findIndex(u => u.toUpperCase() === unit.toUpperCase());
        if (unitIndex === -1) return 0;

        return parseFloat(size) * Math.pow(this.BASE, unitIndex);
    }
} 